using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class ProductBatch
{
    public int Id { get; set; }

    [Display(Name = "Produit")]
    public int ProductId { get; set; }

    [Display(Name = "Produit")]
    public Product? Product { get; set; }

    [Required(ErrorMessage = "Le numéro de lot est obligatoire.")]
    [StringLength(80)]
    [Display(Name = "N° de lot")]
    public string LotNumber { get; set; } = string.Empty;

    [Display(Name = "Date d'expiration")]
    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; }

    [Display(Name = "Quantité restante")]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    [Display(Name = "Ligne d'import source")]
    public int? SourceImportLineId { get; set; }

    public ImportLine? SourceImportLine { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
