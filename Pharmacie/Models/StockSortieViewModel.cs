using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class StockSortieViewModel
{
    [Display(Name = "Lot")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez un lot.")]
    public int BatchId { get; set; }

    [Display(Name = "Quantité")]
    [Range(1, int.MaxValue, ErrorMessage = "La quantité doit être au moins 1.")]
    public int Quantity { get; set; }

    [StringLength(500)]
    [Display(Name = "Motif")]
    public string? Reason { get; set; }
}
