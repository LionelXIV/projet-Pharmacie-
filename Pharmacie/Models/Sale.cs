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

    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
}
