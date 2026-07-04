using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Models.Dto;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = $"{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
public class ProductImportsController : Controller
{
    private const int PreviewPageSize = 50;

    private readonly ApplicationDbContext _db;
    private readonly ProductImportService _importService;

    public ProductImportsController(ApplicationDbContext db, ProductImportService importService)
    {
        _db = db;
        _importService = importService;
    }

    [HttpGet]
    public IActionResult Upload()
    {
        return View(new ProductImportUploadViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(ProductImportUploadViewModel model)
    {
        if (model.File == null || model.File.Length == 0)
            ModelState.AddModelError(nameof(model.File), "Sélectionnez un fichier Excel.");

        if (model.File != null && model.File.Length > 0)
        {
            var extension = Path.GetExtension(model.File.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(model.File), "Seuls les fichiers .xlsx sont acceptés.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var stream = model.File!.OpenReadStream();
        var batchId = await _importService.PrepareImportAsync(stream, model.File.FileName, userId!);

        return RedirectToAction(nameof(Preview), new { id = batchId });
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int id, int page = 1)
    {
        if (page < 1)
            page = 1;

        ImportBatchPreviewSummary summary;
        try
        {
            summary = await _importService.GetPreviewSummaryAsync(id);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        var batchMeta = await _db.ImportBatches
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new { b.Status })
            .FirstOrDefaultAsync();

        var unresolvedBlocking = await _db.ImportAnomalies
            .AsNoTracking()
            .CountAsync(a => a.ImportLine!.ImportBatchId == id
                && a.Severity == ImportAnomalySeverity.Bloquante
                && !a.ResolvedByUser);

        var totalLines = summary.TotalRows;
        var totalPages = totalLines == 0 ? 1 : (int)Math.Ceiling(totalLines / (double)PreviewPageSize);
        if (page > totalPages)
            page = totalPages;

        var lines = await _db.ImportLines
            .AsNoTracking()
            .Where(l => l.ImportBatchId == id)
            .OrderBy(l => l.RowNumber)
            .Skip((page - 1) * PreviewPageSize)
            .Take(PreviewPageSize)
            .Select(l => new ImportLinePreviewRowViewModel
            {
                Id = l.Id,
                RowNumber = l.RowNumber,
                RawCip = l.RawCip,
                RawLibelle = l.RawLibelle,
                RawQtefact = l.RawQtefact,
                RawPxFab = l.RawPxFab,
                RawPph = l.RawPph,
                ResolvedAction = l.ResolvedAction,
                MatchedProductId = l.MatchedProductId,
                AnomalyCount = l.Anomalies.Count,
                BlockingAnomalyCount = l.Anomalies.Count(a => a.Severity == ImportAnomalySeverity.Bloquante),
                WarningCount = l.Anomalies.Count(a => a.Severity == ImportAnomalySeverity.Avertissement)
            })
            .ToListAsync();

        var vm = new ProductImportPreviewViewModel
        {
            ImportBatchId = id,
            Summary = summary,
            Lines = lines,
            CurrentPage = page,
            TotalPages = totalPages,
            BatchStatus = batchMeta?.Status ?? ImportBatchStatus.EnAttenteValidation,
            UnresolvedBlockingAnomaliesCount = unresolvedBlocking
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            await _importService.ConfirmImportAsync(id, userId!);
            return RedirectToAction(nameof(Result), new { id });
        }
        catch (ProductImportUnresolvedAnomaliesException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Anomalies), new { id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Preview), new { id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Result(int id)
    {
        ProductImportResultViewModel vm;
        try
        {
            vm = await _importService.GetImportResultAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Preview), new { id });
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Anomalies(int id)
    {
        var batch = await _db.ImportBatches
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new { b.Id, b.FileName })
            .FirstOrDefaultAsync();

        if (batch == null)
            return NotFound();

        var lines = await _db.ImportLines
            .AsNoTracking()
            .Include(l => l.Anomalies)
            .Where(l => l.ImportBatchId == id
                && l.Anomalies.Any(a => a.Severity == ImportAnomalySeverity.Bloquante && !a.ResolvedByUser))
            .OrderBy(l => l.RowNumber)
            .ToListAsync();

        var vm = new ProductImportAnomalyViewModel
        {
            ImportBatchId = batch.Id,
            FileName = batch.FileName,
            Lines = lines.Select(MapAnomalyRow).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anomalies(ProductImportAnomalyViewModel model)
    {
        var batchExists = await _db.ImportBatches.AnyAsync(b => b.Id == model.ImportBatchId);
        if (!batchExists)
            return NotFound();

        var batchLines = await _db.ImportLines
            .Include(l => l.Anomalies)
            .Where(l => l.ImportBatchId == model.ImportBatchId)
            .OrderBy(l => l.RowNumber)
            .ToListAsync();

        var linesById = batchLines.ToDictionary(l => l.Id);
        var rowsToProcess = new List<(int Index, ProductImportAnomalyRowViewModel Row, ImportLine Line, List<ImportAnomaly> UnresolvedBlocking, bool RequiresReplacementPph)>();

        for (var i = 0; i < model.Lines.Count; i++)
        {
            var row = model.Lines[i];
            if (!linesById.TryGetValue(row.ImportLineId, out var line))
                continue;

            var unresolvedBlocking = line.Anomalies
                .Where(a => a.Severity == ImportAnomalySeverity.Bloquante && !a.ResolvedByUser)
                .ToList();

            if (unresolvedBlocking.Count == 0)
                continue;

            var requiresReplacementPph = unresolvedBlocking
                .Any(a => a.AnomalyType == ImportAnomalyType.PphZeroOuInferieurAuPrixFab);

            if (row.Decision == UserDecision.ForcerImport && requiresReplacementPph)
            {
                if (!row.ReplacementPph.HasValue || row.ReplacementPph.Value <= 0)
                {
                    ModelState.AddModelError(
                        $"{nameof(model.Lines)}[{i}].{nameof(row.ReplacementPph)}",
                        "Un prix de vente (PPH) strictement positif est obligatoire pour forcer l'import.");
                }
            }

            rowsToProcess.Add((i, row, line, unresolvedBlocking, requiresReplacementPph));
        }

        if (!ModelState.IsValid)
        {
            await RepopulateAnomalyViewModelAsync(model, linesById);
            return View(model);
        }

        var processedCount = 0;
        foreach (var (_, row, line, unresolvedBlocking, requiresReplacementPph) in rowsToProcess.OrderBy(x => x.Line.RowNumber))
        {
            var resolutionText = row.Decision switch
            {
                UserDecision.Ignorer => "Ignoré par l'utilisateur",
                UserDecision.ForcerImport when requiresReplacementPph && row.ReplacementPph.HasValue =>
                    $"Import forcé — PPH remplacé par {row.ReplacementPph.Value:0.00}",
                UserDecision.ForcerImport => "Import forcé par l'utilisateur",
                _ => "Décision enregistrée"
            };

            if (row.Decision == UserDecision.ForcerImport && requiresReplacementPph)
                line.RawPph = row.ReplacementPph;

            foreach (var anomaly in unresolvedBlocking)
            {
                anomaly.ResolvedByUser = true;
                anomaly.Resolution = resolutionText;
            }

            if (row.Decision == UserDecision.Ignorer)
            {
                line.ResolvedAction = ImportLineAction.Ignoree;
                line.MatchedProductId = null;
            }
            else
            {
                var (action, matchedProductId) = await _importService.ResolveActionAfterAnomalyResolutionAsync(
                    line,
                    batchLines);
                line.ResolvedAction = action;
                line.MatchedProductId = matchedProductId;
            }

            processedCount++;
        }

        if (processedCount > 0)
            await _db.SaveChangesAsync();

        var remaining = await _db.ImportAnomalies
            .Where(a => a.ImportLine!.ImportBatchId == model.ImportBatchId
                && a.Severity == ImportAnomalySeverity.Bloquante
                && !a.ResolvedByUser)
            .CountAsync();

        if (remaining > 0)
        {
            TempData["Warning"] = $"{processedCount} ligne(s) traitée(s). Il reste {remaining} anomalie(s) bloquante(s) à résoudre.";
            return RedirectToAction(nameof(Anomalies), new { id = model.ImportBatchId });
        }

        TempData["Success"] = "Toutes les anomalies bloquantes ont été résolues. Vous pouvez consulter la prévisualisation mise à jour.";
        return RedirectToAction(nameof(Preview), new { id = model.ImportBatchId });
    }

    private async Task RepopulateAnomalyViewModelAsync(
        ProductImportAnomalyViewModel model,
        Dictionary<int, ImportLine> linesById)
    {
        model.FileName = await _db.ImportBatches
            .AsNoTracking()
            .Where(b => b.Id == model.ImportBatchId)
            .Select(b => b.FileName)
            .FirstOrDefaultAsync();

        for (var i = 0; i < model.Lines.Count; i++)
        {
            var row = model.Lines[i];
            if (!linesById.TryGetValue(row.ImportLineId, out var line))
                continue;

            row.RowNumber = line.RowNumber;
            row.RawCip = line.RawCip;
            row.RawLibelle = line.RawLibelle;
            row.RequiresReplacementPph = line.Anomalies.Any(a =>
                a.Severity == ImportAnomalySeverity.Bloquante
                && !a.ResolvedByUser
                && a.AnomalyType == ImportAnomalyType.PphZeroOuInferieurAuPrixFab);
            row.BlockingAnomalies = line.Anomalies
                .Where(a => a.Severity == ImportAnomalySeverity.Bloquante && !a.ResolvedByUser)
                .Select(a => new ProductImportAnomalyItemViewModel
                {
                    AnomalyType = a.AnomalyType,
                    Details = a.Details
                })
                .ToList();
        }
    }

    private static ProductImportAnomalyRowViewModel MapAnomalyRow(ImportLine line)
    {
        var blocking = line.Anomalies
            .Where(a => a.Severity == ImportAnomalySeverity.Bloquante && !a.ResolvedByUser)
            .ToList();

        return new ProductImportAnomalyRowViewModel
        {
            ImportLineId = line.Id,
            RowNumber = line.RowNumber,
            RawCip = line.RawCip,
            RawLibelle = line.RawLibelle,
            RequiresReplacementPph = blocking
                .Any(a => a.AnomalyType == ImportAnomalyType.PphZeroOuInferieurAuPrixFab),
            BlockingAnomalies = blocking
                .Select(a => new ProductImportAnomalyItemViewModel
                {
                    AnomalyType = a.AnomalyType,
                    Details = a.Details
                })
                .ToList()
        };
    }
}
