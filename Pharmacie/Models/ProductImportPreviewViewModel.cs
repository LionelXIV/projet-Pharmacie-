using Pharmacie.Models.Dto;

namespace Pharmacie.Models;

public class ProductImportPreviewViewModel
{
    public int ImportBatchId { get; set; }

    public ImportBatchPreviewSummary Summary { get; set; } = null!;

    public IReadOnlyList<ImportLinePreviewRowViewModel> Lines { get; set; } = Array.Empty<ImportLinePreviewRowViewModel>();

    public int CurrentPage { get; set; }

    public int TotalPages { get; set; }

    public ImportBatchStatus BatchStatus { get; set; }

    public int UnresolvedBlockingAnomaliesCount { get; set; }

    public string? ActiveFilter { get; set; }

    /// <summary>Nombre de lignes après application du filtre (pour pagination).</summary>
    public int FilteredTotalCount { get; set; }

    public bool CanConfirmImport =>
        BatchStatus == ImportBatchStatus.EnAttenteValidation && UnresolvedBlockingAnomaliesCount == 0;
}
