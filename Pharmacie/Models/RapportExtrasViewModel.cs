namespace Pharmacie.Models;

public class RapportExtrasViewModel
{
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public int NombreVentes { get; set; }
    public decimal CATotal { get; set; }
    public List<Sale> Ventes { get; set; } = new();
}
