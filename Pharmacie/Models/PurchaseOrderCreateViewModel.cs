using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PurchaseOrderCreateViewModel
{
    [Display(Name = "Fournisseur")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez un fournisseur.")]
    public int SupplierId { get; set; }

    [Display(Name = "Date de commande")]
    [DataType(DataType.Date)]
    public DateTime OrderDate { get; set; } = DateTime.Today;

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<PurchaseOrderLineSlotViewModel> Lines { get; set; } = new();
}

public class PurchaseOrderLineSlotViewModel
{
    [Display(Name = "Produit")]
    public int ProductId { get; set; }

    [Display(Name = "Quantité")]
    [Range(0, int.MaxValue)]
    public int QuantityOrdered { get; set; }
}
