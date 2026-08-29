using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom commercial est obligatoire.")]
    [StringLength(200)]
    [Display(Name = "Nom commercial")]
    public string CommercialName { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Nom générique")]
    public string? GenericName { get; set; }

    [Display(Name = "Catégorie")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez une catégorie.")]
    public int CategoryId { get; set; }

    [Display(Name = "Catégorie")]
    public Category? Category { get; set; }

    [StringLength(80)]
    [Display(Name = "Forme")]
    public string? Form { get; set; }

    [StringLength(80)]
    [Display(Name = "Dosage")]
    public string? Dosage { get; set; }

    [Display(Name = "Fournisseur")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez un fournisseur.")]
    public int SupplierId { get; set; }

    [Display(Name = "Fournisseur")]
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix d'achat")]
    [Range(0, 999_999_999.99)]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix de vente")]
    [Range(0, 999_999_999.99)]
    public decimal SalePrice { get; set; }

    [Display(Name = "Quantité en stock")]
    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [Display(Name = "Seuil d'alerte")]
    [Range(0, int.MaxValue)]
    public int AlertThreshold { get; set; }

    /// <summary>Plafond de stock souhaité (0 = non renseigné). Qté à commander = Max − stock actuel.</summary>
    [Display(Name = "Stock maximum")]
    [Range(0, int.MaxValue)]
    public int StockMaximum { get; set; }

    /// <summary>A = forte rotation, B = moyenne, C = faible, D = dormant.</summary>
    [StringLength(1)]
    [Display(Name = "Classe ABC")]
    public string ClasseABC { get; set; } = "C";

    [StringLength(120)]
    [Display(Name = "Emplacement")]
    public string? Location { get; set; }

    [Display(Name = "Actif")]
    public bool IsActive { get; set; } = true;

    /// <summary>Code identifiant de présentation (chaîne, zéros significatifs conservés).</summary>
    [StringLength(20)]
    [Display(Name = "CIP")]
    public string? Cip { get; set; }

    [StringLength(20)]
    [Display(Name = "Référence HA")]
    public string? Refha { get; set; }

    [Display(Name = "Type de produit")]
    public ProductType ProductType { get; set; } = ProductType.Inconnu;

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix d'achat de référence")]
    [Range(0, 999_999_999.99)]
    public decimal? ReferencePurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix de vente réglementé")]
    [Range(0, 999_999_999.99)]
    public decimal? RegulatedSalePrice { get; set; }

    [Display(Name = "Type de tarification")]
    public TarifType TarifType { get; set; } = TarifType.PrixPublicPPH;

    [Column(TypeName = "decimal(18,4)")]
    [Display(Name = "Coefficient")]
    public decimal? Coefficient { get; set; }

    [Display(Name = "Assujetti TVA")]
    public bool AssujettiTVA { get; set; } = false;

    [Column(TypeName = "decimal(5,2)")]
    [Display(Name = "Taux TVA (%)")]
    public decimal TauxTVA { get; set; } = 0;

    /// <summary>Produit boîte parent si ce produit est une unité (vente au détail).</summary>
    [Display(Name = "Produit parent (boîte)")]
    public int? ParentProductId { get; set; }

    [Display(Name = "Produit parent")]
    public Product? ParentProduct { get; set; }

    /// <summary>Produits unités liés à cette boîte.</summary>
    public ICollection<Product> ChildProducts { get; set; } = new List<Product>();

    /// <summary>Nombre d'unités dans une boîte (renseigné sur le produit enfant).</summary>
    [Display(Name = "Unités par boîte")]
    [Range(1, 100_000)]
    public int? NbUnitesParBoite { get; set; }

    /// <summary>True si ce produit (boîte) a un produit unité associé.</summary>
    [Display(Name = "Vente en détail")]
    public bool EstVenteDetail { get; set; }

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
