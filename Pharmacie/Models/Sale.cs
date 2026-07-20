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

    [Display(Name = "Moyen de paiement")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Especes;

    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
}
