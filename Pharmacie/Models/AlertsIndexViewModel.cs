namespace Pharmacie.Models;

public class AlertsIndexViewModel
{
    public int HorizonDays { get; set; } = 90;
    public int? CategorieId { get; set; }

    public List<Product> Ruptures { get; set; } = new();
    public List<Product> StockFaible { get; set; } = new();
    public List<ProductBatch> LotsExpires { get; set; } = new();
    public List<ProductBatch> PeremptionsProches { get; set; } = new();
    public List<Category> Categories { get; set; } = new();

    public DateTime Today { get; set; } = DateTime.Today;
}
