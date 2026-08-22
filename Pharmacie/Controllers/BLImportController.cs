using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = $"{AppRoles.GoodsReceipt},{AppRoles.Administrateur}")]
public class BLImportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BLImportController> _logger;
    private readonly IConfiguration _configuration;

    public BLImportController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<BLImportController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Analyser(IFormFile? fichierPdf)
    {
        if (fichierPdf == null || fichierPdf.Length == 0)
        {
            TempData["Error"] = "Veuillez sélectionner un PDF.";
            return RedirectToAction(nameof(Upload));
        }

        if (!fichierPdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Seuls les fichiers PDF sont acceptés.";
            return RedirectToAction(nameof(Upload));
        }

        try
        {
            await using var memStream = new MemoryStream();
            await fichierPdf.CopyToAsync(memStream);
            var pdfBytes = memStream.ToArray();

            string texteComplet;
            using (var pdfStream = new MemoryStream(pdfBytes, writable: false))
            {
                texteComplet = BlImportService.ExtraireTextePdf(pdfStream);
            }

            var ocrUtilise = false;
            if (string.IsNullOrWhiteSpace(texteComplet) || texteComplet.Trim().Length < 50)
            {
                var visionEndpoint = _configuration["VisionAI:Endpoint"]
                    ?? _configuration["VisionAI__Endpoint"];
                var visionApiKey = _configuration["VisionAI:ApiKey"]
                    ?? _configuration["VisionAI__ApiKey"];

                if (!string.IsNullOrWhiteSpace(visionEndpoint)
                    && !string.IsNullOrWhiteSpace(visionApiKey))
                {
                    try
                    {
                        texteComplet = await BlImportService.ExtraireTexteOCR(
                            pdfBytes, visionEndpoint, visionApiKey);
                        ocrUtilise = true;
                        _logger.LogInformation(
                            "OCR retourné : {Length} caractères pour {File}",
                            texteComplet?.Length ?? 0,
                            fichierPdf.FileName);
                        if (string.IsNullOrWhiteSpace(texteComplet))
                        {
                            TempData["Warning"] =
                                "OCR exécuté mais aucun texte retourné par Azure (réponse vide).";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "OCR Azure Vision indisponible pour {File} ({ExceptionType}): {Message}",
                            fichierPdf.FileName,
                            ex.GetType().Name,
                            ex.Message);
                        TempData["Warning"] =
                            $"OCR indisponible ({ex.GetType().Name}) : {ex.Message}";
                    }
                }
                else
                {
                    TempData["Warning"] =
                        "PDF scanné détecté. Les données seront partielles. Complétez manuellement.";
                }
            }

            var fournisseur = BlImportService.DetecterFournisseur(texteComplet);
            var numeroBL = BlImportService.ExtrairNumeroBL(texteComplet, fournisseur);
            var dateBL = BlImportService.ExtraireDate(texteComplet);

            var lignes = fournisseur == "UbiPharm"
                ? BlImportService.ParserUbiPharm(texteComplet)
                : BlImportService.ParserSodipharm(texteComplet);

            foreach (var ligne in lignes)
            {
                var produit = await MatchProduitAsync(ligne);
                if (produit == null)
                    continue;

                ligne.ProductId = produit.Id;
                ligne.NomCatalogue = produit.CommercialName;
                ligne.PrixCatalogue = produit.PurchasePrice;
                ligne.Trouve = true;
            }

            var vm = new BLImportViewModel
            {
                Fournisseur = fournisseur,
                NumeroBL = numeroBL,
                DateBL = dateBL ?? DateTime.Today,
                Lignes = lignes,
                NomFichier = fichierPdf.FileName,
                NbTrouvees = lignes.Count(l => l.Trouve),
                NbNonTrouvees = lignes.Count(l => !l.Trouve)
            };

            ViewBag.OcrUtilise = ocrUtilise;
            ViewBag.TexteOcrBrut = ocrUtilise ? texteComplet : null;
            return View("Verifier", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lecture PDF BL impossible ({File})", fichierPdf.FileName);
            TempData["Error"] = "Erreur lors de la lecture du PDF : " + ex.Message;
            return RedirectToAction(nameof(Upload));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Pharmacien},{AppRoles.Vendeur},{AppRoles.Administrateur}")]
    public async Task<IActionResult> Confirmer(BLImportConfirmerViewModel model)
    {
        model.Lignes ??= new List<BLImportLigneConfirmer>();
        var userId = _userManager.GetUserId(User) ?? "";
        var nbImportes = 0;
        var nbIgnores = 0;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var ligne in model.Lignes.Where(l => l.ProductId > 0 && l.QuantiteLivree > 0 && l.Importer))
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == ligne.ProductId);
                if (product == null)
                {
                    nbIgnores++;
                    continue;
                }

                var datePeremption = (ligne.DatePeremption ?? DateTime.Today.AddYears(2)).Date;
                var lotNumber = string.IsNullOrWhiteSpace(ligne.NumeroLot)
                    ? $"BL-{model.NumeroBL}-{DateTime.Now:yyyyMMdd}".Trim('-')
                    : ligne.NumeroLot.Trim();
                if (lotNumber.Length > 80)
                    lotNumber = lotNumber[..80];

                var batch = await _context.ProductBatches.FirstOrDefaultAsync(b =>
                    b.ProductId == product.Id
                    && b.LotNumber == lotNumber
                    && b.ExpirationDate.Date == datePeremption);

                if (batch == null)
                {
                    batch = new ProductBatch
                    {
                        ProductId = product.Id,
                        LotNumber = lotNumber,
                        ExpirationDate = datePeremption,
                        Quantity = ligne.QuantiteLivree
                    };
                    _context.ProductBatches.Add(batch);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    batch.Quantity += ligne.QuantiteLivree;
                    await _context.SaveChangesAsync();
                }

                product.StockQuantity += ligne.QuantiteLivree;
                if (ligne.PrixAchat > 0 && ligne.PrixAchat != product.PurchasePrice)
                    product.PurchasePrice = ligne.PrixAchat;

                _context.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    BatchId = batch.Id,
                    Type = StockMovementType.Entree,
                    Quantity = ligne.QuantiteLivree,
                    OccurredAt = DateTime.Now,
                    UserId = userId,
                    Reason = $"Import BL PDF {model.NumeroBL} — {model.Fournisseur}"
                });

                nbImportes++;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] =
                $"BL importé avec succès. {nbImportes} produits mis à jour. {nbIgnores} ignorés.";
            return RedirectToAction("Index", "GoodsReceipts");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Import BL PDF {Numero}", model.NumeroBL);
            TempData["Error"] = "Erreur lors de l'import : " + ex.Message;
            return RedirectToAction(nameof(Upload));
        }
    }

    private async Task<Product?> MatchProduitAsync(BLLigneExtraite ligne)
    {
        var cip = ligne.CIP?.Trim();
        if (!string.IsNullOrEmpty(cip))
        {
            var byCip = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.Cip == cip)
                .FirstOrDefaultAsync();
            if (byCip != null)
                return byCip;
        }

        var nom = ligne.NomProduit?.Trim() ?? "";
        if (nom.Length < 5)
            return null;

        var prefix = nom.Length > 12 ? nom[..12] : nom;
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.CommercialName.Contains(prefix))
            .FirstOrDefaultAsync();
    }
}
