using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class ReglementBon
{
    public int Id { get; set; }

    public int BonId { get; set; }
    public Bon Bon { get; set; } = null!;

    [Display(Name = "Date de règlement")]
    public DateTime DateReglement { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Montant")]
    public decimal Montant { get; set; }

    [Display(Name = "Mode de paiement")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Especes;

    /// <summary>Libellé libre si PaymentMethod == Autre.</summary>
    [StringLength(100)]
    public string? PaymentMethodAutre { get; set; }

    [StringLength(450)]
    public string EncaisseParUserId { get; set; } = "";
}
