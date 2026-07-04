namespace Pharmacie.Models.Dto;

/// <summary>Résultat de validation d'une ligne Excel d'import (pas une entité EF).</summary>
public class ValidatedImportRow
{
    public ExcelImportRow SourceRow { get; set; } = null!;

    public List<DetectedAnomaly> Anomalies { get; set; } = new();
}
