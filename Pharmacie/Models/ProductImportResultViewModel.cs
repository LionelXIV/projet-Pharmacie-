namespace Pharmacie.Models;

public class ProductImportResultViewModel
{
    public int ImportBatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public int ProductsCreatedCount { get; set; }

    public int BatchesCreatedCount { get; set; }

    public int ProductsUpdatedCount { get; set; }

    public int IgnoredLinesCount { get; set; }

    public int TotalAnomaliesCount { get; set; }

    public int ResolvedAnomaliesCount { get; set; }
}
