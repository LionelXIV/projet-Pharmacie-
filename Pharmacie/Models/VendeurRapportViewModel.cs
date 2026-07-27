namespace Pharmacie.Models;

public class VendeurRapportViewModel
{
    public int? VendeurId { get; set; }
    public string NomVendeur { get; set; } = "";
    public string? CouleurTicket { get; set; }
    public int NombreVentes { get; set; }
    public decimal ChiffreAffaires { get; set; }
    public int NombreArticles { get; set; }
    public decimal PanierMoyen { get; set; }
}
