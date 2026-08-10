using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class GoodsReceipt
{
    public int Id { get; set; }

    /// <summary>Null pour un BL saisi directement (sans commande préalable).</summary>
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>Fournisseur du BL direct (si pas de commande).</summary>
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [StringLength(80)]
    [Display(Name = "Référence BL")]
    public string? Reference { get; set; }

    [Display(Name = "Date de réception")]
    public DateTime ReceivedAt { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}
