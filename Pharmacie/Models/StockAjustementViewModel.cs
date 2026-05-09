using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class StockAjustementViewModel
{
    [Display(Name = "Lot")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez un lot.")]
    public int BatchId { get; set; }

    [Display(Name = "Variation (positif = ajout, négatif = retrait)")]
    public int Delta { get; set; }

    [StringLength(500)]
    [Display(Name = "Motif")]
    public string? Reason { get; set; }
}
