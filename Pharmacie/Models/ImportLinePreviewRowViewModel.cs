using Pharmacie.Models.Dto;

namespace Pharmacie.Models;

public class ImportLinePreviewRowViewModel
{
    public int Id { get; set; }

    public int RowNumber { get; set; }

    public string? RawCip { get; set; }

    public string? RawLibelle { get; set; }

    public int? RawQtefact { get; set; }

    public decimal? RawPxFab { get; set; }

    public decimal? RawPph { get; set; }

    public ImportLineAction ResolvedAction { get; set; }

    public int? MatchedProductId { get; set; }

    public int AnomalyCount { get; set; }

    public int BlockingAnomalyCount { get; set; }

    public int WarningCount { get; set; }
}
