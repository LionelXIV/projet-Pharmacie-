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

    [Display(Name = "Montant encaissé")]
    public decimal MontantEncaisse { get; set; }

    public bool PaiementFractionne { get; set; }

    public PaymentMethod? PaymentMethod2 { get; set; }

    public decimal MontantPaiement1 { get; set; }

    public decimal MontantPaiement2 { get; set; }

    [StringLength(100)]
    [Display(Name = "Nom du client")]
    public string? NomClient { get; set; }

    public int? VenteOriginaleId { get; set; }

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

    /// <summary>Prix envoyé depuis le formulaire (saisie vente passée).</summary>
    public decimal UnitPrice { get; set; }

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

    [ValidateNever]
    public bool PrixModifie { get; set; }

    [ValidateNever]
    public decimal AncienPrix { get; set; }
}
