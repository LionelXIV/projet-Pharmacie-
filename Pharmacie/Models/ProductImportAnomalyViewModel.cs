using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum UserDecision
{
    [Display(Name = "Ignorer")]
    Ignorer = 0,

    [Display(Name = "Importer quand même")]
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

    /// <summary>Null tant que l'utilisateur n'a pas choisi.</summary>
    public UserDecision? Decision { get; set; }

    /// <summary>
    /// Vrai si une anomalie bloquante prix de vente zéro nécessite une saisie de prix au forçage.
    /// </summary>
    public bool RequiresReplacementPph { get; set; }

    /// <summary>Vrai si le nom du produit est manquant et peut être saisi ici.</summary>
    public bool RequiresLibelleCorrection { get; set; }

    [Display(Name = "Prix de vente")]
    public decimal? ReplacementPph { get; set; }

    /// <summary>Alias UX / binding alternatif pour le prix de vente de remplacement.</summary>
    [Display(Name = "Prix de vente")]
    public decimal? PphRemplacement
    {
        get => ReplacementPph;
        set => ReplacementPph = value;
    }

    [Display(Name = "Nom du produit")]
    [StringLength(200)]
    public string? LibelleCorrection { get; set; }
}

public class ProductImportAnomalyItemViewModel
{
    public ImportAnomalyType AnomalyType { get; set; }

    public string? Details { get; set; }
}
