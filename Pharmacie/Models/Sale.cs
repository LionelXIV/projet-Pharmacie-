using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class Sale
{
    public int Id { get; set; }

    [Display(Name = "Date de vente")]
    public DateTime SoldAt { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [StringLength(450)]
    [Display(Name = "Utilisateur")]
    public string? UserId { get; set; }

    [Display(Name = "Vendeur")]
    public int? VendeurId { get; set; }

    public Vendeur? Vendeur { get; set; }

    [Display(Name = "Moyen de paiement")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Especes;

    /// <summary>Libellé libre si PaymentMethod == Autre.</summary>
    [StringLength(100)]
    public string? PaymentMethodAutre { get; set; }

    /// <summary>True = vente passée saisie manuellement (régularisation).</summary>
    public bool IsRegularisation { get; set; } = false;

    public bool IsAnnulee { get; set; } = false;

    public DateTime? DateAnnulation { get; set; }

    [StringLength(450)]
    public string? AnnuleeParUserId { get; set; }

    [StringLength(200)]
    public string? AnnuleeParNom { get; set; }

    [StringLength(500)]
    public string? RaisonAnnulation { get; set; }

    /// <summary>True = vente saisie en mode fantôme Administrateur (exclue des rapports).</summary>
    public bool IsAdminTest { get; set; } = false;

    [Display(Name = "Montant encaissé")]
    public decimal MontantEncaisse { get; set; }

    [Display(Name = "Monnaie rendue")]
    public decimal MonnaieRendue { get; set; }

    public bool PaiementFractionne { get; set; }

    [Display(Name = "2e moyen de paiement")]
    public PaymentMethod? PaymentMethod2 { get; set; }

    [Display(Name = "Montant paiement 1")]
    public decimal MontantPaiement1 { get; set; }

    [Display(Name = "Montant paiement 2")]
    public decimal MontantPaiement2 { get; set; }

    [StringLength(100)]
    [Display(Name = "Nom du client")]
    public string? NomClient { get; set; }

    public bool IsModifiee { get; set; }

    public int? VenteOriginaleId { get; set; }

    public int? VenteRemplaceeParId { get; set; }

    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
}
