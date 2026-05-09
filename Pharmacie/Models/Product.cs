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

    [StringLength(120)]
    [Display(Name = "Emplacement")]
    public string? Location { get; set; }

    [Display(Name = "Actif")]
    public bool IsActive { get; set; } = true;

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
