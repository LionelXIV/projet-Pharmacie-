namespace Pharmacie.Models;

public class DashboardViewModel
{
    public DateTime Today { get; set; }

    /// <summary>Tous les produits en base (actifs ou non).</summary>
    public int TotalProducts { get; set; }

    /// <summary>Produits actifs avec quantité ≤ seuil d’alerte.</summary>
    public int LowStockProductsCount { get; set; }

    /// <summary>Produits actifs en rupture (quantité 0).</summary>
    public int OutOfStockProductsCount { get; set; }

    /// <summary>Lots avec stock restant, non expirés, péremption dans la fenêtre (<c>Alerts:ExpirationHorizonDays</c>).</summary>
    public int NearExpiryLotsCount { get; set; }

    /// <summary>Lots avec stock restant déjà expirés.</summary>
    public int ExpiredLotsCount { get; set; }

    /// <summary>Commandes envoyées ou partiellement reçues (en cours).</summary>
    public int PendingPurchaseOrdersCount { get; set; }

    public int SalesTodayCount { get; set; }

    public decimal SalesTodayTotal { get; set; }

    public List<DashboardMovementRow> RecentMovements { get; set; } = new();

    public List<DashboardSaleRow> RecentSales { get; set; } = new();

    public int ExpirationHorizonDays { get; set; }

    /// <summary>Rappels patients à faire (date ≤ aujourd’hui), réservé aux rôles avec accès tableau de bord + module patients.</summary>
    public int PatientRemindersDueCount { get; set; }

    public bool ShowPatientDashboardWidget { get; set; }
}

public class DashboardMovementRow
{
    public int Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string LotLabel { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }

    /// <summary>Utilisateur Identity associé au mouvement (si enregistré).</summary>
    public string ResponsibleDisplay { get; set; } = "—";

    public int? SaleId { get; set; }
}

public class DashboardSaleRow
{
    public int Id { get; set; }
    public DateTime SoldAt { get; set; }
    public int LineCount { get; set; }
    public decimal Total { get; set; }

    /// <summary>Utilisateur ayant enregistré la vente (si renseigné).</summary>
    public string RecordedByDisplay { get; set; } = "—";
}
