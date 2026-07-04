using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum UserDecision
{
    [Display(Name = "Ignorer")]
    Ignorer = 0,

    [Display(Name = "Forcer l'import")]
    ForcerImport = 1
}

public class ProductImportAnomalyViewModel
{
    public int ImportBatchId { get; set; }

    public string? FileName { get; set; }

    public List<ProductImportAnomalyRowViewModel> Lines { get; set; } = new();
}

public class ProductImportAnomalyRowViewModel
{
    public int ImportLineId { get; set; }

    public int RowNumber { get; set; }

    public string? RawCip { get; set; }

    public string? RawLibelle { get; set; }

    public List<ProductImportAnomalyItemViewModel> BlockingAnomalies { get; set; } = new();

    public UserDecision Decision { get; set; } = UserDecision.Ignorer;

    /// <summary>
    /// Vrai si une anomalie bloquante PPH zéro nécessite une saisie de prix au forçage.
    /// </summary>
    public bool RequiresReplacementPph { get; set; }

    [Display(Name = "Prix de vente (PPH)")]
    public decimal? ReplacementPph { get; set; }
}

public class ProductImportAnomalyItemViewModel
{
    public ImportAnomalyType AnomalyType { get; set; }

    public string? Details { get; set; }
}
