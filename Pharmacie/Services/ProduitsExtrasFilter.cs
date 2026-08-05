using Pharmacie.Models;

namespace Pharmacie.Services;

/// <summary>Filtres Produits Extras (catégories hors système).</summary>
public static class ProduitsExtrasFilter
{
    public static bool IsProduitHorsSysteme(Product? product) =>
        product?.Category?.EstHorsSysteme == true;

    public static bool IsLigneHorsSysteme(SaleLine line) =>
        IsProduitHorsSysteme(line.Product);

    public static bool SaleContientExtras(Sale sale) =>
        sale.Lines.Any(IsLigneHorsSysteme);

    public static IEnumerable<Sale> VentesOfficielles(IEnumerable<Sale> ventes) =>
        ventes.Where(s => !SaleContientExtras(s));

    public static IEnumerable<SaleLine> LignesExtras(IEnumerable<SaleLine> lines) =>
        lines.Where(IsLigneHorsSysteme);

    /// <summary>Expression EF : ventes sans aucune ligne Produits Extras.</summary>
    public static IQueryable<Sale> WhereSansExtras(IQueryable<Sale> query) =>
        query.Where(s => !s.Lines.Any(l =>
            l.Product != null
            && l.Product.Category != null
            && l.Product.Category.EstHorsSysteme));

    /// <summary>Expression EF : ventes contenant au moins une ligne Extras.</summary>
    public static IQueryable<Sale> WhereAvecExtras(IQueryable<Sale> query) =>
        query.Where(s => s.Lines.Any(l =>
            l.Product != null
            && l.Product.Category != null
            && l.Product.Category.EstHorsSysteme));
}
