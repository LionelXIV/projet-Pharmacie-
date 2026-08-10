using System.Globalization;

namespace Pharmacie.Models;

public class EtatTVAViewModel
{
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }

    public List<EtatTVALigneViewModel> Lignes { get; set; } = new();
    public decimal TotalExonere { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }

    public string PeriodeLabel =>
        $"DU {DateDebut:dd/MM/yyyy} AU {DateFin:dd/MM/yyyy}";

    /// <summary>Conservé pour compatibilité affichage (mois de début).</summary>
    public int Mois => DateDebut.Month;
    public int Annee => DateDebut.Year;

    public string MoisAnnee =>
        DateDebut.ToString("MMMM yyyy", new CultureInfo("fr-FR"));

    public DateTime DebutPeriode => DateDebut.Date;
    public DateTime FinPeriode => DateFin.Date;
}

public class EtatTVALigneViewModel
{
    public DateTime Date { get; set; }
    public decimal MontantExonere { get; set; }
    public decimal MontantHT { get; set; }
    public decimal MontantTVA { get; set; }
    public decimal MontantTTC { get; set; }
}
