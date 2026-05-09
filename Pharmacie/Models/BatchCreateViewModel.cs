using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class BatchCreateViewModel
{
    [Display(Name = "Produit")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez un produit.")]
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Le numéro de lot est obligatoire.")]
    [StringLength(80)]
    [Display(Name = "N° de lot")]
    public string LotNumber { get; set; } = string.Empty;

    [Display(Name = "Date d'expiration")]
    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; } = DateTime.Today.AddMonths(6);

    [Display(Name = "Quantité")]
    [Range(1, int.MaxValue, ErrorMessage = "La quantité doit être au moins 1.")]
    public int Quantity { get; set; }

    [StringLength(500)]
    [Display(Name = "Motif / commentaire")]
    public string? Reason { get; set; }
}
