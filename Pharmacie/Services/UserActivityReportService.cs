using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class UserActivityReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Dictionary<string, string> RoleLabels = new()
    {
        [AppRoles.PharmacienTitulaire] = "Pharmacien Titulaire",
        [AppRoles.Administrateur] = "Pharmacien Titulaire",
        [AppRoles.Pharmacien] = "Pharmacien",
        [AppRoles.Vendeur] = "Vendeur",
        [AppRoles.GestionnaireStock] = "Vendeur",
        [AppRoles.AssistantPharmacien] = "Assistant Pharmacien",
        [AppRoles.Assistant] = "Assistant Pharmacien",
        [AppRoles.Caissier] = "Caissier",
        [AppRoles.Stagiaire] = "Stagiaire"
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserActivityReportService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<UserActivityReport> GenerateReportAsync(string userId, string deletedByUserId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var deletedBy = await _userManager.FindByIdAsync(deletedByUserId);
        var roles = await _userManager.GetRolesAsync(user);
        var isAdmin = AppRoles.HasTitulaireRole(roles);
        var connectionType = isAdmin || string.IsNullOrEmpty(user.PinHash) ? "Email" : "PIN";
        var roleLabel = string.Join(", ", roles.Select(AppRoles.GetRoleLabel).Distinct().OrderBy(r => r));
        if (string.IsNullOrEmpty(roleLabel))
            roleLabel = "—";

        var sales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.SoldAt)
            .ToListAsync();

        var movements = await _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Batch)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync();

        // PurchaseOrder / GoodsReceipt n'ont pas de UserId en schéma actuel.
        var orders = new List<PurchaseOrder>();
        var receipts = new List<GoodsReceipt>();

        var imports = await _context.ImportBatches
            .AsNoTracking()
            .Where(b => b.UploadedByUserId == userId || b.ConfirmedByUserId == userId)
            .OrderBy(b => b.UploadedAt)
            .ToListAsync();

        var saleDtos = sales.Select(s => new UserActivitySaleDto
        {
            Id = s.Id,
            SoldAt = s.SoldAt,
            Total = s.Lines.Sum(l => l.UnitPrice * l.Quantity),
            PaymentMethod = s.PaymentMethod switch
            {
                PaymentMethod.Wave => "Wave",
                PaymentMethod.OrangeMoney => "Orange Money",
                _ => "Espèces"
            },
            Products = s.Lines.Select(l => new UserActivitySaleLineDto
            {
                CommercialName = l.Product?.CommercialName ?? "—",
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            }).ToList()
        }).ToList();

        var movementDtos = movements.Select(m => new UserActivityMovementDto
        {
            Id = m.Id,
            OccurredAt = m.OccurredAt,
            Type = m.Type switch
            {
                StockMovementType.Entree => "Entrée",
                StockMovementType.Sortie => "Sortie",
                StockMovementType.Ajustement => "Ajustement",
                _ => m.Type.ToString()
            },
            Quantity = m.Quantity,
            Product = m.Product?.CommercialName ?? "—",
            LotNumber = m.Batch?.LotNumber,
            Reason = m.Reason
        }).ToList();

        var importDtos = imports.Select(b => new UserActivityImportDto
        {
            Id = b.Id,
            FileName = b.FileName,
            UploadedAt = b.UploadedAt,
            Role = b.UploadedByUserId == userId && b.ConfirmedByUserId == userId
                ? "Téléversement + confirmation"
                : b.UploadedByUserId == userId
                    ? "Téléversement"
                    : "Confirmation",
            Status = b.Status.ToString()
        }).ToList();

        var activityDates = saleDtos.Select(s => (DateTime?)s.SoldAt)
            .Concat(movementDtos.Select(m => (DateTime?)m.OccurredAt))
            .Concat(importDtos.Select(i => (DateTime?)i.UploadedAt))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        var totalSalesAmount = saleDtos.Sum(s => s.Total);
        var first = activityDates.Count > 0 ? activityDates.Min() : (DateTime?)null;
        var last = activityDates.Count > 0 ? activityDates.Max() : (DateTime?)null;

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? (user.Email ?? user.UserName ?? user.Id)
            : user.DisplayName;
        var deletedByDisplay = deletedBy == null
            ? "—"
            : (string.IsNullOrWhiteSpace(deletedBy.DisplayName)
                ? (deletedBy.Email ?? deletedBy.UserName ?? deletedBy.Id)
                : deletedBy.DisplayName);

        var reportData = new UserActivityReportData
        {
            User = new UserActivityReportUserSection
            {
                DisplayName = displayName,
                Email = user.Email ?? "",
                Role = roleLabel,
                ConnectionType = connectionType
            },
            Summary = new UserActivityReportSummarySection
            {
                TotalSales = saleDtos.Count,
                TotalSalesAmount = totalSalesAmount,
                TotalMovements = movementDtos.Count,
                TotalOrders = orders.Count,
                TotalReceipts = receipts.Count,
                TotalImports = importDtos.Count,
                FirstActivity = first,
                LastActivity = last
            },
            Sales = saleDtos,
            Movements = movementDtos,
            PurchaseOrders = orders.Select(o => new UserActivityPurchaseOrderDto
            {
                Id = o.Id,
                OrderDate = o.OrderDate,
                Status = o.Status.ToString(),
                Supplier = o.Supplier?.Name ?? "—"
            }).ToList(),
            GoodsReceipts = receipts.Select(r => new UserActivityGoodsReceiptDto
            {
                Id = r.Id,
                ReceivedAt = r.ReceivedAt,
                OrderId = r.PurchaseOrderId
            }).ToList(),
            Imports = importDtos
        };

        return new UserActivityReport
        {
            DeletedUserId = user.Id,
            DeletedUserDisplayName = displayName,
            DeletedUserEmail = user.Email ?? "",
            DeletedUserRole = roleLabel,
            DeletedUserConnectionType = connectionType,
            DeletedByUserId = deletedByUserId,
            DeletedByDisplayName = deletedByDisplay,
            DeletedAt = DateTime.UtcNow,
            ActivityReportJson = JsonSerializer.Serialize(reportData, JsonOptions),
            TotalSales = saleDtos.Count,
            TotalSalesAmount = totalSalesAmount,
            TotalStockMovements = movementDtos.Count,
            TotalGoodsReceipts = receipts.Count,
            TotalPurchaseOrders = orders.Count,
            FirstActivityDate = first,
            LastActivityDate = last
        };
    }

    public static UserActivityReportData? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<UserActivityReportData>(json, JsonOptions);
    }
}
