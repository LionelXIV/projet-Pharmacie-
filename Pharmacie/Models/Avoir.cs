using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class Avoir
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string Numero { get; set; } = "";

    [Required]
    [StringLength(200)]
    [Display(Name = "Nom du client")]
    public string ClientNom { get; set; } = "";

    [StringLength(30)]
    [Display(Name = "Téléphone client")]
    public string? ClientTelephone { get; set; }

    [StringLength(50)]
    [Display(Name = "N° pièce d'identité")]
    public string? NumeroIdentite { get; set; }

    [Display(Name = "Date de création")]
    public DateTime DateCreation { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Montant total")]
    public decimal MontantTotal { get; set; }

    [Display(Name = "Statut")]
    public AvoirStatut Statut { get; set; } = AvoirStatut.EnAttente;

    [Display(Name = "Mode de paiement")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Especes;

    [StringLength(450)]
    public string CreatedByUserId { get; set; } = "";

    [Display(Name = "Vendeur")]
    public int? VendeurId { get; set; }
    public Vendeur? Vendeur { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public ICollection<AvoirLigne> Lignes { get; set; } = new List<AvoirLigne>();
}

public enum AvoirStatut
{
    [Display(Name = "En attente")]
    EnAttente = 0,

    [Display(Name = "Livré")]
    Livre = 1,

    [Display(Name = "Annulé")]
    Annule = 2
}
