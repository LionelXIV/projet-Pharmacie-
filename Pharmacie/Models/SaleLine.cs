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
}
