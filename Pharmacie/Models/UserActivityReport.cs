using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class UserActivityReport
{
    public int Id { get; set; }

    [StringLength(450)]
    public string DeletedUserId { get; set; } = "";

    [StringLength(100)]
    public string DeletedUserDisplayName { get; set; } = "";

    [StringLength(256)]
    public string DeletedUserEmail { get; set; } = "";

    [StringLength(100)]
    public string DeletedUserRole { get; set; } = "";

    /// <summary>"Email" ou "PIN"</summary>
    [StringLength(20)]
    public string DeletedUserConnectionType { get; set; } = "";

    [StringLength(450)]
    public string DeletedByUserId { get; set; } = "";

    [StringLength(100)]
    public string DeletedByDisplayName { get; set; } = "";

    public DateTime DeletedAt { get; set; }

    public string ActivityReportJson { get; set; } = "";

    public int TotalSales { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSalesAmount { get; set; }

    public int TotalStockMovements { get; set; }

    public int TotalGoodsReceipts { get; set; }

    public int TotalPurchaseOrders { get; set; }

    public DateTime? FirstActivityDate { get; set; }

    public DateTime? LastActivityDate { get; set; }
}
