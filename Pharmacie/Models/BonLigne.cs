using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class BonLigne
{
    public int Id { get; set; }

    public int BonId { get; set; }
    public Bon Bon { get; set; } = null!;

    [Display(Name = "Produit")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Quantité")]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix unitaire")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercent { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [StringLength(20)]
    public string DiscountType { get; set; } = "";

    [NotMapped]
    public decimal LineTotal
    {
        get
        {
            var base_ = UnitPrice * Quantity;
            if (DiscountType == "percent" && DiscountPercent > 0)
                return base_ * (1 - DiscountPercent / 100);
            if (DiscountType == "amount" && DiscountAmount > 0)
                return Math.Max(0, base_ - DiscountAmount);
            return base_;
        }
    }
}
