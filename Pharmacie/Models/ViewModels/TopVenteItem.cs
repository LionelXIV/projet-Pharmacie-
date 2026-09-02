namespace Pharmacie.Models.ViewModels;

public class TopVenteItem
{
    public int ProductId { get; set; }
    public string Nom { get; set; } = string.Empty;
    public decimal PrixCession { get; set; }
    public decimal PrixVenteMoyen { get; set; }
    public int QuantiteVendue { get; set; }
    public decimal CA { get; set; }
    public decimal Marge { get; set; }
    public decimal TauxMarge =>
        CA > 0
            ? Math.Round(Marge / CA * 100, 1)
            : 0;
}

public class VenteParJourItem
{
    public DateTime Jour { get; set; }
    public int NbVentes { get; set; }
    public decimal CA { get; set; }
}

public class AnalyseVenteLigne
{
    public int ProductId { get; set; }
    public string Nom { get; set; } = string.Empty;
    public int Total { get; set; }
    public Dictionary<DateTime, int> QuantitesParJour { get; set; } = new();

    public int QteDuJour(DateTime jour) =>
        QuantitesParJour.TryGetValue(jour.Date, out var qte) ? qte : 0;
}

public class AnalyseVenteVm
{
    public List<DateTime> Jours { get; set; } = new();
    public List<AnalyseVenteLigne> Lignes { get; set; } = new();
}

public class BlProduitRechercheItem
{
    public int BlId { get; set; }
    public string? BlRef { get; set; }
    public string Fournisseur { get; set; } = "—";
    public DateTime DateReception { get; set; }
    public string Produit { get; set; } = string.Empty;
    public string? Cip { get; set; }
    public int Quantite { get; set; }
    public string? Lot { get; set; }
    public DateTime Peremption { get; set; }
}
