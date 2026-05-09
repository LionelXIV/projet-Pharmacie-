using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Tests.Support;

/// <summary>
/// Données minimales pour tester ventes et réceptions (catégorie, fournisseur, produit actif).
/// </summary>
internal static class TestCatalogSeed
{
    public static (Category Category, Supplier Supplier, Product Product) SeedBasicCatalog(ApplicationDbContext db)
    {
        var category = new Category { Name = "Test — Catégorie" };
        var supplier = new Supplier { Name = "Test — Fournisseur" };
        db.Categories.Add(category);
        db.Suppliers.Add(supplier);
        db.SaveChanges();

        var product = new Product
        {
            CommercialName = "Produit test",
            CategoryId = category.Id,
            SupplierId = supplier.Id,
            PurchasePrice = 1m,
            SalePrice = 10m,
            StockQuantity = 0,
            AlertThreshold = 2,
            IsActive = true
        };
        db.Products.Add(product);
        db.SaveChanges();
        return (category, supplier, product);
    }
}
