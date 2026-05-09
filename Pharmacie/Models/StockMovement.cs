using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class StockMovement
{
    public int Id { get; set; }

    [Display(Name = "Produit")]
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    [Display(Name = "Lot")]
    public int BatchId { get; set; }

    public ProductBatch? Batch { get; set; }

    [Display(Name = "Type")]
    public StockMovementType Type { get; set; }

    /// <summary>
    /// Entrée / sortie : quantité positive. Ajustement : variation (peut être négative).
    /// </summary>
    [Display(Name = "Quantité / variation")]
    public int Quantity { get; set; }

    [StringLength(500)]
    [Display(Name = "Motif")]
    public string? Reason { get; set; }

    [Display(Name = "Date")]
    public DateTime OccurredAt { get; set; }

    [StringLength(450)]
    [Display(Name = "Utilisateur")]
    public string? UserId { get; set; }

    /// <summary>
    /// Si renseigné, mouvement issu d'une vente (sorties FIFO).
    /// </summary>
    [Display(Name = "Vente")]
    public int? SaleId { get; set; }

    public Sale? Sale { get; set; }
}
