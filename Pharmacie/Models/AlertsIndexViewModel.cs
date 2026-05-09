namespace Pharmacie.Models;

public class AlertsIndexViewModel
{
    public const int ExpirationHorizonDays = 90;

    public List<Product> LowStockProducts { get; set; } = new();

    /// <summary>Lots avec stock restant et date d'expiration dans la fenêtre (ordonnés par date).</summary>
    public List<ProductBatch> BatchesNearingExpiration { get; set; } = new();

    public DateTime Today { get; set; } = DateTime.Today;
}
