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

    [Range(0, int.MaxValue)]
    public int QuantiteLivree { get; set; }

    [StringLength(80)]
    public string? NumeroLot { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DatePeremption { get; set; }

    [Range(0, 999_999_999.99)]
    public decimal PrixAchat { get; set; }

    [Range(0, 999_999_999.99)]
    public decimal PrixVente { get; set; }

    /// <summary>Si true et produit sans enfant, crée le produit unité.</summary>
    public bool CreerVenteDetail { get; set; }

    [Range(1, 100_000)]
    public int? NbUnitesParBoite { get; set; }

    [Range(0, 999_999_999.99)]
    public decimal? PrixUnite { get; set; }

    /// <summary>Active la saisie d'unités gratuites (promo fournisseur).</summary>
    public bool EstUG { get; set; }

    /// <summary>Nombre d'unités gratuites (en plus de la qté payante). Entre en stock, hors achat/TVA.</summary>
    [Range(0, int.MaxValue)]
    public int NbUG { get; set; }

    /// <summary>Taux TVA (%) de la ligne (affiché / résumé ; prérempli depuis le produit).</summary>
    [Range(0, 100)]
    public decimal TauxTVA { get; set; }

    /// <summary>Boîtes du lot reçu à convertir immédiatement en unités/tablettes.</summary>
    [Range(0, int.MaxValue)]
    public int NbBoitesAOuvrir { get; set; }
}

public class GoodsReceiptEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "N° BL fournisseur")]
    [StringLength(80)]
    public string? Reference { get; set; }

    [Display(Name = "Fournisseur")]
    public int? SupplierId { get; set; }

    [Display(Name = "Date de réception")]
    [DataType(DataType.Date)]
    public DateTime DateReception { get; set; }

    [Display(Name = "Notes")]
    [StringLength(500)]
    public string? Notes { get; set; }

    public List<GoodsReceiptEditLigneViewModel> Lignes { get; set; } = new();
}

public class GoodsReceiptEditLigneViewModel
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string NomProduit { get; set; } = "";
    public int QuantiteRecue { get; set; }

    [StringLength(80)]
    [Display(Name = "N° lot")]
    public string? NumeroLot { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Péremption")]
    public DateTime? DatePeremption { get; set; }
}
