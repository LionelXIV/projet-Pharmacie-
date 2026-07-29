using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class SaleLine
{
    public int Id { get; set; }

    public int SaleId { get; set; }

    public Sale? Sale { get; set; }

    [Display(Name = "Produit")]
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    [Display(Name = "Quantité")]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix unitaire")]
    public decimal UnitPrice { get; set; }

    /// <summary>Remise en pourcentage (0 si remise en montant ou pas de remise).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercent { get; set; } = 0;

    /// <summary>Remise en montant fixe FCFA (0 si remise en % ou pas de remise).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    /// <summary>"percent", "amount" ou "" si pas de remise.</summary>
    [StringLength(20)]
    public string DiscountType { get; set; } = "";
}
