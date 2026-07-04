namespace Pharmacie.Models.Dto;

/// <summary>Ligne brute lue depuis un fichier Excel d'import produits (pas une entité EF).</summary>
public class ExcelImportRow
{
    public int RowNumber { get; set; }

    public string? Cip { get; set; }

    public string? Refha { get; set; }

    public string? Libelle { get; set; }

    public int? Qtefact { get; set; }

    public decimal? PxFab { get; set; }

    public decimal? Pph { get; set; }
}
