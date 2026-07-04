using Pharmacie.Models;

namespace Pharmacie.Models.Dto;

/// <summary>Anomalie détectée lors de la validation d'une ligne d'import (pas une entité EF).</summary>
public class DetectedAnomaly
{
    public ImportAnomalyType AnomalyType { get; set; }

    public ImportAnomalySeverity Severity { get; set; }

    public string? Details { get; set; }
}
