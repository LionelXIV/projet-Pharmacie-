namespace Pharmacie.Models;

public class SuggestionCommandeItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int StockActuel { get; set; }
    public int StockMinimum { get; set; }
    public int QuantiteConseillee { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "Non défini";
    public string Statut { get; set; } = "";
    public bool Selectionne { get; set; } = true;
}

public class SuggestionFournisseurGroupe
{
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "Non défini";
    public List<SuggestionCommandeItem> Items { get; set; } = new();
}
