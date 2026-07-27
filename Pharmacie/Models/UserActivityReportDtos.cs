namespace Pharmacie.Models;

/// <summary>Structure JSON archivée dans <see cref="UserActivityReport.ActivityReportJson"/>.</summary>
public class UserActivityReportData
{
    public UserActivityReportUserSection User { get; set; } = new();
    public UserActivityReportSummarySection Summary { get; set; } = new();
    public List<UserActivitySaleDto> Sales { get; set; } = new();
    public List<UserActivityMovementDto> Movements { get; set; } = new();
    public List<UserActivityPurchaseOrderDto> PurchaseOrders { get; set; } = new();
    public List<UserActivityGoodsReceiptDto> GoodsReceipts { get; set; } = new();
    public List<UserActivityImportDto> Imports { get; set; } = new();
}

public class UserActivityReportUserSection
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string ConnectionType { get; set; } = "";
}

public class UserActivityReportSummarySection
{
    public int TotalSales { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public int TotalMovements { get; set; }
    public int TotalOrders { get; set; }
    public int TotalReceipts { get; set; }
    public int TotalImports { get; set; }
    public DateTime? FirstActivity { get; set; }
    public DateTime? LastActivity { get; set; }
}

public class UserActivitySaleDto
{
    public int Id { get; set; }
    public DateTime SoldAt { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "";
    public List<UserActivitySaleLineDto> Products { get; set; } = new();
}

public class UserActivitySaleLineDto
{
    public string CommercialName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UserActivityMovementDto
{
    public int Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = "";
    public int Quantity { get; set; }
    public string Product { get; set; } = "";
    public string? LotNumber { get; set; }
    public string? Reason { get; set; }
}

public class UserActivityPurchaseOrderDto
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "";
    public string Supplier { get; set; } = "";
}

public class UserActivityGoodsReceiptDto
{
    public int Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public int OrderId { get; set; }
}

public class UserActivityImportDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
}

public class DeleteWithReportViewModel
{
    public string UserId { get; set; } = "";
    public UserActivityReport Preview { get; set; } = new();
}
