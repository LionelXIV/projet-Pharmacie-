using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class ImportBatch
{
    public int Id { get; set; }

    [Required]
    [StringLength(260)]
    [Display(Name = "Fichier")]
    public string FileName { get; set; } = string.Empty;

    [Display(Name = "Téléversé le")]
    public DateTime UploadedAt { get; set; }

    [StringLength(450)]
    [Display(Name = "Téléversé par")]
    public string? UploadedByUserId { get; set; }

    [Display(Name = "Statut")]
    public ImportBatchStatus Status { get; set; }

    [Display(Name = "Nombre de lignes")]
    public int TotalRows { get; set; }

    [Display(Name = "Confirmé le")]
    public DateTime? ConfirmedAt { get; set; }

    [StringLength(450)]
    [Display(Name = "Confirmé par")]
    public string? ConfirmedByUserId { get; set; }

    public ICollection<ImportLine> Lines { get; set; } = new List<ImportLine>();
}
