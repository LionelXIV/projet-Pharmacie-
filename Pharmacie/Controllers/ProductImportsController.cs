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

[Authorize(Roles = $"{AppRoles.CanManageUsers},{AppRoles.Administrateur}")]
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
    public async Task<IActionResult> Preview(int id, int page = 1, string? filter = null)
    {
        if (page < 1)
            page = 1;

        filter = NormalizePreviewFilter(filter);

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

        var linesQuery = _db.ImportLines
            .AsNoTracking()
            .Where(l => l.ImportBatchId == id);

        linesQuery = filter switch
        {
            "anomalies" => linesQuery.Where(l => l.Anomalies.Count > 0),
            "creations" => linesQuery.Where(l => l.ResolvedAction == ImportLineAction.CreationProduit),
            "mises-a-jour" => linesQuery.Where(l => l.ResolvedAction == ImportLineAction.MiseAJourPrix),
            _ => linesQuery
        };

        var filteredTotal = await linesQuery.CountAsync();
        var totalPages = filteredTotal == 0 ? 1 : (int)Math.Ceiling(filteredTotal / (double)PreviewPageSize);
        if (page > totalPages)
            page = totalPages;

        var lines = await linesQuery
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

        ViewBag.ActiveFilter = filter;

        var vm = new ProductImportPreviewViewModel
        {
            ImportBatchId = id,
            Summary = summary,
            Lines = lines,
            CurrentPage = page,
            TotalPages = totalPages,
            BatchStatus = batchMeta?.Status ?? ImportBatchStatus.EnAttenteValidation,
            UnresolvedBlockingAnomaliesCount = unresolvedBlocking,
            ActiveFilter = filter,
            FilteredTotalCount = filteredTotal
        };

        return View(vm);
    }

    private static string? NormalizePreviewFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        return filter.Trim().ToLowerInvariant() switch
        {
            "anomalies" => "anomalies",
            "creations" => "creations",
            "mises-a-jour" => "mises-a-jour",
            _ => null
        };
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceConfirmAll(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            await _importService.ForceConfirmAllAsync(id, userId!);
            TempData["Success"] =
                "Import forcé effectué. Vérifiez les produits sans prix dans le catalogue.";
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
        var rowsToProcess = new List<(int Index, ProductImportAnomalyRowViewModel Row, ImportLine Line, List<ImportAnomaly> UnresolvedBlocking, bool RequiresReplacementPph, bool RequiresLibelleCorrection)>();

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

            if (!row.Decision.HasValue)
            {
                ModelState.AddModelError(
                    $"{nameof(model.Lines)}[{i}].{nameof(row.Decision)}",
                    "Choisissez Ignorer ou Importer quand même pour cette ligne.");
            }

            var requiresReplacementPph = unresolvedBlocking
                .Any(a => a.AnomalyType == ImportAnomalyType.PphZeroOuInferieurAuPrixFab);
            var requiresLibelleCorrection = unresolvedBlocking
                .Any(a => a.AnomalyType == ImportAnomalyType.LibelleVide);

            if (row.Decision == UserDecision.ForcerImport && requiresReplacementPph)
            {
                if (!row.ReplacementPph.HasValue || row.ReplacementPph.Value <= 0)
                {
                    ModelState.AddModelError(
                        $"{nameof(model.Lines)}[{i}].{nameof(row.ReplacementPph)}",
                        "Saisissez un prix de vente strictement positif pour importer cette ligne.");
                }
            }

            if (row.Decision == UserDecision.ForcerImport && requiresLibelleCorrection)
            {
                if (string.IsNullOrWhiteSpace(row.LibelleCorrection))
                {
                    ModelState.AddModelError(
                        $"{nameof(model.Lines)}[{i}].{nameof(row.LibelleCorrection)}",
                        "Saisissez le nom du produit pour importer cette ligne.");
                }
            }

            rowsToProcess.Add((i, row, line, unresolvedBlocking, requiresReplacementPph, requiresLibelleCorrection));
        }

        if (!ModelState.IsValid)
        {
            await RepopulateAnomalyViewModelAsync(model, linesById);
            return View(model);
        }

        var processedCount = 0;
        foreach (var (_, row, line, unresolvedBlocking, requiresReplacementPph, requiresLibelleCorrection) in rowsToProcess.OrderBy(x => x.Line.RowNumber))
        {
            if (row.Decision == UserDecision.ForcerImport)
            {
                if (!string.IsNullOrWhiteSpace(row.LibelleCorrection))
                    line.RawLibelle = row.LibelleCorrection.Trim();

                if (row.ReplacementPph.HasValue && row.ReplacementPph.Value > 0)
                    line.RawPph = row.ReplacementPph;
            }

            var resolutionText = row.Decision switch
            {
                UserDecision.Ignorer => "Ignoré par l'utilisateur",
                UserDecision.ForcerImport when requiresReplacementPph && row.ReplacementPph.HasValue =>
                    $"Import forcé — prix de vente remplacé par {row.ReplacementPph.Value:0.00}",
                UserDecision.ForcerImport when requiresLibelleCorrection && !string.IsNullOrWhiteSpace(row.LibelleCorrection) =>
                    $"Import forcé — nom corrigé : {row.LibelleCorrection.Trim()}",
                UserDecision.ForcerImport => "Import forcé par l'utilisateur",
                _ => "Décision enregistrée"
            };

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
            TempData["Warning"] = $"{processedCount} ligne(s) traitée(s). Il reste {remaining} erreur(s) à corriger.";
            return RedirectToAction(nameof(Anomalies), new { id = model.ImportBatchId });
        }

        TempData["Success"] = "Toutes les erreurs ont été traitées. Vous pouvez consulter la prévisualisation mise à jour.";
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
            row.RequiresLibelleCorrection = line.Anomalies.Any(a =>
                a.Severity == ImportAnomalySeverity.Bloquante
                && !a.ResolvedByUser
                && a.AnomalyType == ImportAnomalyType.LibelleVide);
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
            Decision = null,
            RequiresReplacementPph = blocking
                .Any(a => a.AnomalyType == ImportAnomalyType.PphZeroOuInferieurAuPrixFab),
            RequiresLibelleCorrection = blocking
                .Any(a => a.AnomalyType == ImportAnomalyType.LibelleVide),
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
