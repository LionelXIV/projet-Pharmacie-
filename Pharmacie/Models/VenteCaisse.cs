namespace Pharmacie.Models;

/// <summary>Liaison entre une vente (Sale) et une session de caisse.</summary>
public class VenteCaisse
{
    public int Id { get; set; }

    public int SessionCaisseId { get; set; }
    public SessionCaisse SessionCaisse { get; set; } = null!;

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
}
