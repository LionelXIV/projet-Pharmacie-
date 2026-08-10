namespace Pharmacie.Models;

public class EcartDetailViewModel
{
    public string ProduitBoite { get; set; } = "";
    public string ProduitUnite { get; set; } = "";
    public int NbUnitesParBoite { get; set; }
    public int BoitesOuvertes { get; set; }
    public int UnitesTheorique { get; set; }
    public int UnitesVendues { get; set; }
    public int StockUnitesActuel { get; set; }
    public int Ecart { get; set; }

    /// <summary>Écart négatif = unités vendues sans boîtes ouvertes enregistrées (suspect).</summary>
    public bool EstSuspect => Ecart < 0;
}
