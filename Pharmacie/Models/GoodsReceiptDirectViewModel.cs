using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class GoodsReceiptDirectViewModel
{
    [Display(Name = "N° BL fournisseur")]
    [StringLength(80)]
    public string? Reference { get; set; }

    [Display(Name = "Fournisseur")]
    public int? SupplierId { get; set; }

    [Display(Name = "Date de réception")]
    [DataType(DataType.Date)]
    public DateTime DateReception { get; set; } = DateTime.Today;

    [Display(Name = "Notes")]
    [StringLength(500)]
    public string? Notes { get; set; }

    public List<GoodsReceiptDirectLigne> Lignes { get; set; } = new();
}

public class GoodsReceiptDirectLigne
{
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int QuantiteLivree { get; set; }

    [StringLength(80)]
    public string? NumeroLot { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DatePeremption { get; set; }

    [Range(0, 999_999_999.99)]
    public decimal PrixAchat { get; set; }

    /// <summary>Si true et produit sans enfant, crée le produit unité.</summary>
    public bool CreerVenteDetail { get; set; }

    [Range(1, 100_000)]
    public int? NbUnitesParBoite { get; set; }

    [Range(0, 999_999_999.99)]
    public decimal? PrixUnite { get; set; }
}
