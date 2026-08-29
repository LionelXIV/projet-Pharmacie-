namespace Pharmacie.Models;

public class InventaireItemViewModel
{
    public int ProductId { get; set; }
    public string Nom { get; set; } = "";
    public string? Cip { get; set; }
    public string Categorie { get; set; } = "Sans catégorie";
    public int StockLogiciel { get; set; }
    public int? StockPhysique { get; set; }
    public int? Ecart { get; set; }
}

public class InventaireAjustement
{
    public int ProductId { get; set; }
    public int StockLogiciel { get; set; }
    public int? StockPhysique { get; set; }
}
