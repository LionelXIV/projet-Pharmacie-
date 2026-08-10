namespace Pharmacie.Models.Dto;

/// <summary>Ligne brute lue depuis un fichier BL (Excel/CSV), avant rapprochement catalogue.</summary>
public class BlImportRawRow
{
    public int RowNumber { get; set; }
    public string? Cip { get; set; }
    public string? Libelle { get; set; }
    public int? Quantite { get; set; }
    public decimal? PrixAchat { get; set; }
    public decimal? PrixVente { get; set; }
    public string? NumeroLot { get; set; }
    public DateTime? DatePeremption { get; set; }
    public bool EstUG { get; set; }
    public int? NbUG { get; set; }
    public decimal? TauxTVA { get; set; }
}

/// <summary>Ligne prête à préremplir le formulaire CreateDirect.</summary>
public class BlImportPreviewLine
{
    public int RowNumber { get; set; }
    public int? ProductId { get; set; }
    public string? ProductText { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? TauxTVA { get; set; }
    public int Quantite { get; set; }
    public decimal PrixAchat { get; set; }
    public decimal PrixVente { get; set; }
    public string? NumeroLot { get; set; }
    public string? DatePeremption { get; set; }
    public bool EstUG { get; set; }
    public int NbUG { get; set; }
    public bool Matched { get; set; }
    public string? Warning { get; set; }
}

public class BlImportPreviewResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public List<BlImportPreviewLine> Lines { get; set; } = new();
}
