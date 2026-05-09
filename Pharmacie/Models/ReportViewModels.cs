namespace Pharmacie.Models;

public static class ReportLimits
{
    public const int MaxSalesRows = 500;
    public const int MaxMovementRows = 1000;
}

public class ReportStockStatusRowViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int AlertThreshold { get; set; }

    /// <summary>Normal, Stock faible, ou Rupture.</summary>
    public string StatusLabel { get; set; } = string.Empty;

    public string StatusBadgeClass { get; set; } = "bg-secondary";
}

public class ReportNearExpirationRowViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public int QuantityRemaining { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int DaysRemaining { get; set; }
}

public class ReportExpiredLotRowViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public int QuantityRemaining { get; set; }
    public DateTime ExpirationDate { get; set; }
}

public class ReportSaleHistoryRowViewModel
{
    public int SaleId { get; set; }
    public DateTime SoldAt { get; set; }
    public int LineCount { get; set; }
    public decimal Total { get; set; }
}

public class ReportMovementHistoryRowViewModel
{
    public DateTime OccurredAt { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public string UserOrResponsible { get; set; } = string.Empty;

    /// <summary>Présent si le mouvement est une ligne de déstockage liée à une vente.</summary>
    public int? SaleId { get; set; }

    public string? Reason { get; set; }
}
