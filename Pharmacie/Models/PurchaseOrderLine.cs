using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PurchaseOrderLine
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    [Display(Name = "Produit")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Qté commandée")]
    [Range(1, int.MaxValue)]
    public int QuantityOrdered { get; set; }

    [Display(Name = "Qté reçue")]
    [Range(0, int.MaxValue)]
    public int QuantityReceived { get; set; }
}
