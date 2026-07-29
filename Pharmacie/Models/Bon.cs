using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class Bon
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

    [Display(Name = "Date de création")]
    public DateTime DateCreation { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Montant total")]
    public decimal MontantTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Montant réglé")]
    public decimal MontantRegle { get; set; } = 0;

    [NotMapped]
    [Display(Name = "Montant restant")]
    public decimal MontantRestant => MontantTotal - MontantRegle;

    [Display(Name = "Statut")]
    public BonStatut Statut { get; set; } = BonStatut.Ouvert;

    [StringLength(450)]
    public string CreatedByUserId { get; set; } = "";

    [Display(Name = "Vendeur")]
    public int? VendeurId { get; set; }
    public Vendeur? Vendeur { get; set; }

    [Display(Name = "Notes")]
    [StringLength(500)]
    public string? Notes { get; set; }

    public ICollection<BonLigne> Lignes { get; set; } = new List<BonLigne>();
    public ICollection<ReglementBon> Reglements { get; set; } = new List<ReglementBon>();
}

public enum BonStatut
{
    [Display(Name = "Ouvert")]
    Ouvert = 0,

    [Display(Name = "Partiellement réglé")]
    PartiellemntRegle = 1,

    [Display(Name = "Soldé")]
    Solde = 2,

    [Display(Name = "Annulé")]
    Annule = 3
}
