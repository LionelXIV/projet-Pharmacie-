namespace Pharmacie.Models.ViewModels;

public class SuggestionItem
{
    public int ProductId { get; set; }
    public string Nom { get; set; } = "";
    public int StockActuel { get; set; }
    public int Seuil { get; set; }
    public int QteVendue { get; set; }
    public string Fournisseur { get; set; } = "Sans fournisseur";
    public string Source { get; set; } = "";
}
