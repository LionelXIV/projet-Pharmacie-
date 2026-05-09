using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class GoodsReceipt
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    [Display(Name = "Date de réception")]
    public DateTime ReceivedAt { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}
