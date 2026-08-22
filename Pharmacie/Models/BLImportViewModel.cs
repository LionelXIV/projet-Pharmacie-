namespace Pharmacie.Models;

public class BLLigneExtraite
{
    public string CIP { get; set; } = "";
    public string NomProduit { get; set; } = "";
    public decimal PrixAchat { get; set; }
    public decimal PrixVente { get; set; }
    public decimal? TauxTVA { get; set; }
    public int? QuantiteLivree { get; set; }
    public string? NumeroLot { get; set; }
    public DateTime? DatePeremption { get; set; }
    /// <summary>bonne = extrait fiable, partielle = à compléter, manuelle = non extrait.</summary>
    public string Confiance { get; set; } = "";
    public int? ProductId { get; set; }
    public string? NomCatalogue { get; set; }
    public decimal? PrixCatalogue { get; set; }
    public bool Trouve { get; set; }
}

public class BLImportViewModel
{
    public string Fournisseur { get; set; } = "";
    public string NumeroBL { get; set; } = "";
    public DateTime DateBL { get; set; } = DateTime.Today;
    public string NomFichier { get; set; } = "";
    public int NbTrouvees { get; set; }
    public int NbNonTrouvees { get; set; }
    public List<BLLigneExtraite> Lignes { get; set; } = new();
}

public class BLImportConfirmerViewModel
{
    public string Fournisseur { get; set; } = "";
    public string NumeroBL { get; set; } = "";
    public DateTime DateBL { get; set; }
    public List<BLImportLigneConfirmer> Lignes { get; set; } = new();
}

public class BLImportLigneConfirmer
{
    public int ProductId { get; set; }
    public string NomProduit { get; set; } = "";
    public int QuantiteLivree { get; set; }
    public string? NumeroLot { get; set; }
    public DateTime? DatePeremption { get; set; }
    public decimal PrixAchat { get; set; }
    public bool Importer { get; set; } = true;
}
