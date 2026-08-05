using System.Globalization;

namespace Pharmacie.Models;

public class EtatTVAViewModel
{
    public int Mois { get; set; }
    public int Annee { get; set; }
    public List<EtatTVALigneViewModel> Lignes { get; set; } = new();
    public decimal TotalExonere { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }

    public string MoisAnnee =>
        new DateTime(Annee, Mois, 1).ToString("MMMM yyyy", new CultureInfo("fr-FR"));

    public DateTime DebutPeriode => new(Annee, Mois, 1);
    public DateTime FinPeriode => DebutPeriode.AddMonths(1).AddDays(-1);
}

public class EtatTVALigneViewModel
{
    public DateTime Date { get; set; }
    public decimal MontantExonere { get; set; }
    public decimal MontantHT { get; set; }
    public decimal MontantTVA { get; set; }
    public decimal MontantTTC { get; set; }
}
