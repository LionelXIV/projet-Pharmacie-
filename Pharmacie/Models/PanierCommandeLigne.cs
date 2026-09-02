using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PanierCommandeLigne
{
    public int Id { get; set; }

    public int PanierCommandeId { get; set; }

    public PanierCommande? PanierCommande { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public int QuantiteConseillee { get; set; }

    public int QuantiteFinale { get; set; }

    [Required]
    [StringLength(80)]
    public string Source { get; set; } = string.Empty;

    public bool Selectionne { get; set; } = true;

    public DateTime AddedAt { get; set; } = DateTime.Now;
}

public class PanierFournisseurGroupe
{
    public string Nom { get; set; } = "Sans fournisseur";
    public List<PanierCommandeLigne> Lignes { get; set; } = new();
    public decimal Total => Lignes.Sum(l =>
        l.QuantiteFinale * (l.Product?.PurchasePrice ?? 0m));
}
