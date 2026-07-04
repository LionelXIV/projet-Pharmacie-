namespace Pharmacie.Models;

public class AlertsIndexViewModel
{
    /// <summary>Default horizon (appsettings <c>Alerts:ExpirationHorizonDays</c>) for Dashboard, Batches, Reports.</summary>
    public static int ExpirationHorizonDays => 90;

    /// <summary>Effective horizon for the Alerts/Index page (may differ via query string).</summary>
    public int HorizonDays { get; set; } = ExpirationHorizonDays;

    public List<Product> LowStockProducts { get; set; } = new();

    /// <summary>Lots avec stock restant et date d'expiration dans la fenêtre (ordonnés par date).</summary>
    public List<ProductBatch> BatchesNearingExpiration { get; set; } = new();

    public DateTime Today { get; set; } = DateTime.Today;
}
