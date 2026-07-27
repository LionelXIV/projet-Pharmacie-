using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Administrateur)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult ResetData()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllData(string confirmation)
    {
        if (!string.Equals(confirmation, "CONFIRMER", StringComparison.Ordinal))
        {
            TempData["Error"] = "Confirmation incorrecte. Tapez exactement CONFIRMER pour continuer.";
            return RedirectToAction(nameof(ResetData));
        }

        var adminName = User.Identity?.Name ?? "Inconnu";

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Ordre des dépendances FK (voir ApplicationDbContext / Scripts/reset-production-data.sql)
            await _context.ImportAnomalies.ExecuteDeleteAsync();
            await _context.ImportLines.ExecuteDeleteAsync();
            await _context.ImportBatches.ExecuteDeleteAsync();
            await _context.UserActivityReports.ExecuteDeleteAsync();
            await _context.PatientTreatmentReminders.ExecuteDeleteAsync();
            await _context.PatientPrescriptions.ExecuteDeleteAsync();
            await _context.Patients.ExecuteDeleteAsync();
            await _context.SaleLines.ExecuteDeleteAsync();
            await _context.Sales.ExecuteDeleteAsync();
            await _context.GoodsReceiptLines.ExecuteDeleteAsync();
            await _context.GoodsReceipts.ExecuteDeleteAsync();
            await _context.PurchaseOrderLines.ExecuteDeleteAsync();
            await _context.PurchaseOrders.ExecuteDeleteAsync();
            await _context.StockMovements.ExecuteDeleteAsync();
            await _context.ProductBatches.ExecuteDeleteAsync();
            await _context.Products.ExecuteDeleteAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Échec de la remise à zéro des données par {Admin}", adminName);
            TempData["Error"] = "La remise à zéro a échoué. Aucune donnée n'a été modifiée. Consultez les journaux.";
            return RedirectToAction(nameof(ResetData));
        }

        _logger.LogWarning(
            "RESET COMPLET effectué par {Admin} le {Date:o}",
            adminName,
            DateTime.UtcNow);

        TempData["Success"] =
            "Remise à zéro effectuée. La base de données est propre. Les utilisateurs et vendeurs sont conservés.";

        return RedirectToAction("Index", "Dashboard");
    }
}
