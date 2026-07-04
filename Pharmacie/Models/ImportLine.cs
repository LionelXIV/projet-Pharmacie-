using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class ImportLine
{
    public int Id { get; set; }

    [Display(Name = "Lot d'import")]
    public int ImportBatchId { get; set; }

    public ImportBatch? ImportBatch { get; set; }

    [Display(Name = "N° de ligne")]
    public int RowNumber { get; set; }

    [StringLength(200)]
    [Display(Name = "CIP brut")]
    public string? RawCip { get; set; }

    [StringLength(200)]
    [Display(Name = "Réf. HA brute")]
    public string? RawRefha { get; set; }

    [StringLength(200)]
    [Display(Name = "Libellé brut")]
    public string? RawLibelle { get; set; }

    [Display(Name = "Qté facturée brute")]
    public int? RawQtefact { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix fab. brut")]
    public decimal? RawPxFab { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "PPH brut")]
    public decimal? RawPph { get; set; }

    [Display(Name = "Action résolue")]
    public ImportLineAction ResolvedAction { get; set; } = ImportLineAction.Anomalie;

    [Display(Name = "Produit associé")]
    public int? MatchedProductId { get; set; }

    public Product? MatchedProduct { get; set; }

    [Display(Name = "Lot créé")]
    public int? CreatedBatchId { get; set; }

    public ProductBatch? CreatedBatch { get; set; }

    public ICollection<ImportAnomaly> Anomalies { get; set; } = new List<ImportAnomaly>();
}
