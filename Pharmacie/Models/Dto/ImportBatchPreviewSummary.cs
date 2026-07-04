namespace Pharmacie.Models.Dto;

/// <summary>Compteurs agrégés d'un lot d'import en prévisualisation (pas une entité EF).</summary>
public class ImportBatchPreviewSummary
{
    public int ImportBatchId { get; set; }

    public int TotalRows { get; set; }

    public int CreationProduitCount { get; set; }

    public int MiseAJourPrixCount { get; set; }

    public int NouveauLotCount { get; set; }

    public int IgnoreeCount { get; set; }

    public int AnomalieCount { get; set; }

    public int TotalAnomaliesCount { get; set; }

    public int BlockingAnomaliesCount { get; set; }

    public int WarningAnomaliesCount { get; set; }
}
