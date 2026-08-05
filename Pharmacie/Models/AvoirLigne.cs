using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class AvoirLigne
{
    public int Id { get; set; }

    public int AvoirId { get; set; }
    public Avoir Avoir { get; set; } = null!;

    [Display(Name = "Produit")]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Display(Name = "Quantité")]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Prix unitaire")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Livré")]
    public bool EstLivre { get; set; } = false;

    [Display(Name = "Date de livraison")]
    public DateTime? DateLivraison { get; set; }

    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;
}
