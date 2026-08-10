using Pharmacie.Models;

namespace Pharmacie.Services;

/// <summary>Filtres Produits Extras (catégories hors système) — filtre ligne par ligne.</summary>
public static class ProduitsExtrasFilter
{
    public static bool IsProduitHorsSysteme(Product? product) =>
        product?.Category?.EstHorsSysteme == true;

    public static bool IsLigneHorsSysteme(SaleLine line) =>
        IsProduitHorsSysteme(line.Product);

    public static bool IsLigneOfficielle(SaleLine line) =>
        !IsLigneHorsSysteme(line);

    public static bool SaleContientExtras(Sale sale) =>
        sale.Lines.Any(IsLigneHorsSysteme);

    /// <summary>Lignes hors Produits Extras (rapports officiels).</summary>
    public static IEnumerable<SaleLine> LignesOfficielles(IEnumerable<SaleLine> lignes) =>
        lignes.Where(IsLigneOfficielle);

    /// <summary>Lignes Produits Extras uniquement.</summary>
    public static IEnumerable<SaleLine> LignesExtras(IEnumerable<SaleLine> lignes) =>
        lignes.Where(IsLigneHorsSysteme);

    /// <summary>Ventes ayant au moins une ligne officielle (stats nb ventes / panier).</summary>
    public static IEnumerable<Sale> VentesAvecLignesOfficielles(IEnumerable<Sale> ventes) =>
        ventes.Where(s => s.Lines.Any(IsLigneOfficielle));

    /// <summary>CA des lignes officielles uniquement.</summary>
    public static decimal CaOfficiel(IEnumerable<SaleLine> lignes) =>
        LignesOfficielles(lignes).Sum(l => l.UnitPrice * l.Quantity);

    /// <summary>Expression EF : ventes contenant au moins une ligne Extras.</summary>
    public static IQueryable<Sale> WhereAvecExtras(IQueryable<Sale> query) =>
        query.Where(s => s.Lines.Any(l =>
            l.Product != null
            && l.Product.Category != null
            && l.Product.Category.EstHorsSysteme));
}
