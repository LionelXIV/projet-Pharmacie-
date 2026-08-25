using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class SessionCaisse
{
    public int Id { get; set; }

    /// <summary>1 = Matin, 2 = Soir</summary>
    [Display(Name = "N° caisse")]
    public int NumeroCaisse { get; set; }

    [NotMapped]
    public string NomCaisse => NumeroCaisse == 1 ? "Caisse Matin" : "Caisse Soir";

    [Display(Name = "Date de session")]
    [Column(TypeName = "date")]
    public DateTime DateSession { get; set; } = DateTime.Today;

    [Display(Name = "Heure d'ouverture")]
    public DateTime HeureOuverture { get; set; } = DateTime.Now;

    [Display(Name = "Heure de fermeture")]
    public DateTime? HeureFermeture { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Fond de départ")]
    public decimal FondDepart { get; set; } = 50000;

    [StringLength(450)]
    public string CaissierUserId { get; set; } = "";

    [Display(Name = "Statut")]
    public SessionCaisseStatut Statut { get; set; } = SessionCaisseStatut.Ouverte;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BilletageTotal { get; set; }

    public string? BilletageJson { get; set; }

    [StringLength(200)]
    public string? BilletageModifiePar { get; set; }

    public DateTime? BilletageModifieAt { get; set; }

    [StringLength(500)]
    public string? BilletageRaisonModification { get; set; }

    public ICollection<VenteCaisse> Ventes { get; set; } = new List<VenteCaisse>();
    public ICollection<DepotCaisse> Depots { get; set; } = new List<DepotCaisse>();
}

public enum SessionCaisseStatut
{
    [Display(Name = "Ouverte")]
    Ouverte = 0,

    [Display(Name = "Fermée")]
    Fermee = 1
}
