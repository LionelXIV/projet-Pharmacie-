using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class ImportAnomaly
{
    public int Id { get; set; }

    [Display(Name = "Ligne d'import")]
    public int ImportLineId { get; set; }

    public ImportLine? ImportLine { get; set; }

    [Display(Name = "Type d'anomalie")]
    public ImportAnomalyType AnomalyType { get; set; }

    [Display(Name = "Gravité")]
    public ImportAnomalySeverity Severity { get; set; }

    [StringLength(500)]
    [Display(Name = "Détails")]
    public string? Details { get; set; }

    [Display(Name = "Résolue par l'utilisateur")]
    public bool ResolvedByUser { get; set; }

    [StringLength(500)]
    [Display(Name = "Résolution")]
    public string? Resolution { get; set; }
}
