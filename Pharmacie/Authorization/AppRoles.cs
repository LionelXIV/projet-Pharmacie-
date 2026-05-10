using System.Security.Claims;

namespace Pharmacie.Authorization;

/// <summary>Noms des rôles (alignés sur le semis en base au démarrage).</summary>
public static class AppRoles
{
    public const string Administrateur = "Administrateur";
    public const string Pharmacien = "Pharmacien";
    public const string Assistant = "Assistant";
    public const string GestionnaireStock = "GestionnaireStock";
    public const string Caissier = "Caissier";

    /// <summary>Rôles assignables (ordre d’affichage dans les listes admin).</summary>
    public static readonly string[] AllAssignableRoles =
    [
        Administrateur,
        Pharmacien,
        Assistant,
        GestionnaireStock,
        Caissier
    ];

    /// <summary>Vente et consultation des ventes (large pour les petites équipes).</summary>
    public const string Sales = $"{Administrateur},{Pharmacien},{Assistant},{GestionnaireStock},{Caissier}";

    /// <summary>Lots et mouvements de stock.</summary>
    public const string Inventory = $"{Administrateur},{Pharmacien},{GestionnaireStock}";

    /// <summary>Produits, catégories, fournisseurs.</summary>
    public const string Catalog = $"{Administrateur},{Pharmacien},{GestionnaireStock}";

    /// <summary>Commandes fournisseurs et réceptions.</summary>
    public const string Purchasing = $"{Administrateur},{Pharmacien},{GestionnaireStock}";

    /// <summary>Tableau de bord (indicateurs globaux).</summary>
    public const string DashboardAccess = $"{Administrateur},{Pharmacien},{GestionnaireStock}";

    /// <summary>Rapports (même périmètre que le tableau de bord).</summary>
    public const string ReportsAccess = DashboardAccess;

    /// <summary>Portefeuille patients : consultation (Admin, Pharmacien, Assistant).</summary>
    public const string PatientsRead = $"{Administrateur},{Pharmacien},{Assistant}";

    /// <summary>Portefeuille patients : création / modification / suppression.</summary>
    public const string PatientsManage = $"{Administrateur},{Pharmacien}";

    public static bool CanAccessPatientsRead(ClaimsPrincipal user) =>
        user.IsInRole(Administrateur)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Assistant);

    public static bool CanManagePatients(ClaimsPrincipal user) =>
        user.IsInRole(Administrateur)
        || user.IsInRole(Pharmacien);

    public static bool CanAccessDashboard(ClaimsPrincipal user) =>
        user.IsInRole(Administrateur)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(GestionnaireStock);

    public static bool CanAccessReports(ClaimsPrincipal user) => CanAccessDashboard(user);

    public static bool CanAccessSales(ClaimsPrincipal user) =>
        user.IsInRole(Administrateur)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Assistant)
        || user.IsInRole(GestionnaireStock)
        || user.IsInRole(Caissier);

    public static bool CanAccessPurchasing(ClaimsPrincipal user) =>
        user.IsInRole(Administrateur)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(GestionnaireStock);

    public static bool CanAccessCatalog(ClaimsPrincipal user) =>
        user.IsInRole(Administrateur)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(GestionnaireStock);

    /// <summary>Au moins une entrée du menu « Achats &amp; ventes » est visible.</summary>
    public static bool CanSeeCommerceMenu(ClaimsPrincipal user) =>
        CanAccessSales(user) || CanAccessPurchasing(user) || CanAccessCatalog(user);
}
