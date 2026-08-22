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

    [StringLength(100)]
    [Display(Name = "Précision mode de paiement")]
    public string? PaymentMethodAutre { get; set; }

    public bool IsRegularisation { get; set; }

    [Required(ErrorMessage = "Veuillez sélectionner le vendeur.")]
    [Display(Name = "Vendeur")]
    public int? VendeurId { get; set; }

    public List<SaleLineSlotViewModel> Lines { get; set; } = new() { new SaleLineSlotViewModel() };
}

public class SaleLineSlotViewModel
{
    [Display(Name = "Produit")]
    [ValidateNever]
    public int ProductId { get; set; }

    [Display(Name = "Quantité")]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    /// <summary>Affichage uniquement — pas persisté par le POST POS.</summary>
    [ValidateNever]
    public decimal DisplayPrice { get; set; }

    [ValidateNever]
    public string? ProductName { get; set; }

    [ValidateNever]
    public decimal DiscountPercent { get; set; }

    [ValidateNever]
    public decimal DiscountAmount { get; set; }

    [ValidateNever]
    public string? DiscountType { get; set; }
}
