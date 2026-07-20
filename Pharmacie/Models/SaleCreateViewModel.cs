using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Pharmacie.Models;

public class SaleCreateViewModel
{
    [Display(Name = "Date de vente")]
    [DataType(DataType.DateTime)]
    public DateTime SoldAt { get; set; } = DateTime.Now;

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Moyen de paiement")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Especes;

    public List<SaleLineSlotViewModel> Lines { get; set; } = new();
}

public class SaleLineSlotViewModel
{
    [Display(Name = "Produit")]
    [ValidateNever]
    public int ProductId { get; set; }

    [Display(Name = "Quantité")]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}
