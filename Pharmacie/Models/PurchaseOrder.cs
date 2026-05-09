using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PurchaseOrder
{
    public int Id { get; set; }

    [Display(Name = "Fournisseur")]
    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    [Display(Name = "Date de commande")]
    [DataType(DataType.Date)]
    public DateTime OrderDate { get; set; }

    [Display(Name = "Statut")]
    public PurchaseOrderStatus Status { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<GoodsReceipt> Receipts { get; set; } = new List<GoodsReceipt>();
}
