using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class DepotCaisse
{
    public int Id { get; set; }

    public int SessionCaisseId { get; set; }
    public SessionCaisse SessionCaisse { get; set; } = null!;

    [Display(Name = "Heure du dépôt")]
    public DateTime HeureDepot { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Montant déposé")]
    public decimal MontantDepose { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Solde avant dépôt")]
    public decimal SoldeAvantDepot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Solde après dépôt")]
    public decimal SoldeApresDepot { get; set; }

    [Display(Name = "Type")]
    public DepotCaisseType Type { get; set; } = DepotCaisseType.Normal;

    [StringLength(450)]
    public string EffectueParUserId { get; set; } = "";
}

public enum DepotCaisseType
{
    [Display(Name = "Dépôt")]
    Normal = 0,

    [Display(Name = "Dépôt final")]
    Final = 1
}
