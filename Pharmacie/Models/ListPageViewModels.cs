using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

// --- Produits ---

public class ProductListFilters
{
    /// <summary>Recherche dans le nom commercial et le nom générique.</summary>
    public string? Q { get; set; }

    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }

    /// <summary>Vide = tous. <c>low</c> = actifs avec 0 &lt; stock ≤ seuil. <c>out</c> = actifs en rupture (stock 0).</summary>
    public string? Stock { get; set; }

    /// <summary>Vide = tous, <c>1</c> = actifs uniquement, <c>0</c> = inactifs uniquement.</summary>
    public string? Active { get; set; }
}

public class ProductIndexPageViewModel
{
    public ProductListFilters Filter { get; set; } = new();
    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
    public IReadOnlyList<Category> CategoryLookup { get; set; } = Array.Empty<Category>();
    public IReadOnlyList<Supplier> SupplierLookup { get; set; } = Array.Empty<Supplier>();
}

// --- Lots ---

public class BatchListFilters
{
    public int? ProductId { get; set; }

    /// <summary>Fragment du numéro de lot (contient).</summary>
    public string? Lot { get; set; }

    /// <summary>Vide = tous. <c>near</c> = péremption dans la fenêtre alertes, qté &gt; 0. <c>expired</c> = péremption dépassée, qté &gt; 0.</summary>
    public string? Expiration { get; set; }
}

public class BatchIndexPageViewModel
{
    public BatchListFilters Filter { get; set; } = new();
    public IReadOnlyList<ProductBatch> Batches { get; set; } = Array.Empty<ProductBatch>();
    public IReadOnlyList<Product> ProductLookup { get; set; } = Array.Empty<Product>();
}

// --- Ventes ---

public class SaleListFilters
{
    [Display(Name = "Du")]
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [Display(Name = "Au")]
    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    /// <summary>Identifiant Identity de l’utilisateur ayant enregistré la vente.</summary>
    public string? UserId { get; set; }
}

public class SaleIndexPageViewModel
{
    public SaleListFilters Filter { get; set; } = new();
    public IReadOnlyList<Sale> Sales { get; set; } = Array.Empty<Sale>();
}

// --- Mouvements ---

public class StockMovementListFilters
{
    public int? ProductId { get; set; }

    public StockMovementType? Type { get; set; }

    [Display(Name = "Du")]
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [Display(Name = "Au")]
    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public string? UserId { get; set; }
}

public class StockMovementIndexPageViewModel
{
    public StockMovementListFilters Filter { get; set; } = new();
    public IReadOnlyList<StockMovement> Movements { get; set; } = Array.Empty<StockMovement>();
    public IReadOnlyList<Product> ProductLookup { get; set; } = Array.Empty<Product>();
}

// --- Commandes ---

public class PurchaseOrderListFilters
{
    public int? SupplierId { get; set; }
    public PurchaseOrderStatus? Status { get; set; }
}

public class PurchaseOrderIndexPageViewModel
{
    public PurchaseOrderListFilters Filter { get; set; } = new();
    public IReadOnlyList<PurchaseOrder> Orders { get; set; } = Array.Empty<PurchaseOrder>();
    public IReadOnlyList<Supplier> SupplierLookup { get; set; } = Array.Empty<Supplier>();
}
