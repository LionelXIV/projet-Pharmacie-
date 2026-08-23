using System.Security.Claims;

namespace Pharmacie.Authorization;

/// <summary>Noms des rôles et groupes d'autorisation.</summary>
public static class AppRoles
{
    public const string PharmacienTitulaire = "PharmacienTitulaire";
    public const string Pharmacien = "Pharmacien";
    public const string Vendeur = "Vendeur";
    public const string Caissier = "Caissier";
    public const string AssistantPharmacien = "AssistantPharmacien";
    public const string Stagiaire = "Stagiaire";
    public const string Comptable = "Comptable";

    /// <summary>Anciens rôles conservés en base pendant la transition.</summary>
    public const string Administrateur = "Administrateur";
    public const string Assistant = "Assistant";
    public const string GestionnaireStock = "GestionnaireStock";

    public static readonly string[] AllAssignableRoles =
    [
        PharmacienTitulaire,
        Pharmacien,
        Vendeur,
        Caissier,
        AssistantPharmacien,
        Stagiaire,
        Comptable
    ];

    public static readonly string[] LegacyRoles =
    [
        Administrateur,
        Assistant,
        GestionnaireStock
    ];

    public const string CanSell =
        $"{PharmacienTitulaire},{Administrateur},{Pharmacien},{Caissier},{AssistantPharmacien},{Vendeur}";

    public const string CanManageStock =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur}";

    public const string CanAccessFinance =
        $"{PharmacienTitulaire},{Comptable}";

    public const string CanManageUsers =
        $"{PharmacienTitulaire},{Pharmacien}";

    public const string CanAccessCaisse =
        $"{PharmacienTitulaire},{Administrateur},{Pharmacien},{Caissier},{Vendeur}";

    public const string CanCreateBon =
        $"{PharmacienTitulaire},{Administrateur},{Pharmacien},{Caissier},{AssistantPharmacien},{Vendeur}";

    public const string CanReceiveBL =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur},{Stagiaire}";

    public const string CanModifyPrice =
        $"{PharmacienTitulaire},{Pharmacien},{Caissier},{AssistantPharmacien}";

    public const string CatalogRead =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur},{Caissier},{AssistantPharmacien},{Stagiaire}";

    public const string CatalogManage =
        $"{PharmacienTitulaire},{Pharmacien},{Caissier},{AssistantPharmacien}";

    public const string AlertsAccess =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur},{Caissier},{AssistantPharmacien}";

    // Alias utilisés par les [Authorize] existants
    public const string Sales = CanSell;
    public const string Inventory = CanManageStock;
    public const string Catalog = CatalogManage;
    public const string Purchasing = CanManageStock;
    public const string GoodsReceipt = CanReceiveBL;
    public const string DashboardAccess =
        $"{PharmacienTitulaire},{Administrateur},{Pharmacien},{Caissier},{AssistantPharmacien}";
    public const string ReportsAccess =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur},{Caissier},{AssistantPharmacien},{Comptable}";
    /// <summary>État du stock et péremption (inclut Comptable).</summary>
    public const string StockReportsAccess =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur},{Comptable}";
    /// <summary>Historiques opérationnels (sans Comptable).</summary>
    public const string OperationalReportsAccess =
        $"{PharmacienTitulaire},{Pharmacien},{Vendeur},{Caissier},{AssistantPharmacien}";
    public const string FinancesAccess = CanAccessFinance;
    public const string PatientsRead =
        $"{PharmacienTitulaire},{Pharmacien},{AssistantPharmacien}";
    public const string PatientsManage =
        $"{PharmacienTitulaire},{Pharmacien}";

    public static bool IsTitulaire(ClaimsPrincipal user) =>
        user.IsInRole(PharmacienTitulaire) || user.IsInRole(Administrateur);

    public static bool IsTitulaireRole(string role) =>
        role is PharmacienTitulaire or Administrateur;

    public static bool HasTitulaireRole(IEnumerable<string> roles) =>
        roles.Any(IsTitulaireRole);

    public static bool CanAccessPatientsRead(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant);

    public static bool CanManagePatients(ClaimsPrincipal user) =>
        IsTitulaire(user) || user.IsInRole(Pharmacien);

    public static bool CanAccessDashboard(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Caissier)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant);

    public static bool CanAccessReports(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Vendeur)
        || user.IsInRole(Caissier)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant)
        || user.IsInRole(GestionnaireStock)
        || user.IsInRole(Comptable);

    public static bool CanAccessStockReports(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Vendeur)
        || user.IsInRole(GestionnaireStock)
        || user.IsInRole(Comptable);

    public static bool CanAccessOperationalReports(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Vendeur)
        || user.IsInRole(Caissier)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant)
        || user.IsInRole(GestionnaireStock);

    public static bool CanAccessFinances(ClaimsPrincipal user) =>
        IsTitulaire(user) || user.IsInRole(Comptable);

    public static bool CanManageUsersAccounts(ClaimsPrincipal user) =>
        IsTitulaire(user) || user.IsInRole(Pharmacien);

    public static bool CanAccessSales(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Caissier)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant)
        || user.IsInRole(Vendeur);

    public static bool CanAccessCaisseMenu(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Caissier)
        || user.IsInRole(Vendeur);

    public static bool CanAccessPurchasing(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Vendeur)
        || user.IsInRole(GestionnaireStock);

    public static bool CanAccessGoodsReceipts(ClaimsPrincipal user) =>
        CanAccessPurchasing(user) || user.IsInRole(Stagiaire);

    public static bool CanAccessCatalog(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Vendeur)
        || user.IsInRole(Caissier)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant)
        || user.IsInRole(GestionnaireStock)
        || user.IsInRole(Stagiaire);

    public static bool CanManageCatalog(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Caissier)
        || user.IsInRole(AssistantPharmacien)
        || user.IsInRole(Assistant);

    public static bool CanAccessStock(ClaimsPrincipal user) =>
        IsTitulaire(user)
        || user.IsInRole(Pharmacien)
        || user.IsInRole(Vendeur)
        || user.IsInRole(GestionnaireStock);

    public static bool CanSeeCommerceMenu(ClaimsPrincipal user) =>
        CanAccessSales(user) || CanAccessPurchasing(user) || CanAccessGoodsReceipts(user) || CanAccessCaisseMenu(user);

    public static string GetRoleLabel(string role) => role switch
    {
        PharmacienTitulaire or Administrateur => "Pharmacien Titulaire",
        Pharmacien => "Pharmacien",
        Vendeur or GestionnaireStock => "Vendeur",
        Caissier => "Caissier",
        AssistantPharmacien or Assistant => "Assistant Pharmacien",
        Stagiaire => "Stagiaire",
        Comptable => "Comptable",
        _ => role
    };
}
