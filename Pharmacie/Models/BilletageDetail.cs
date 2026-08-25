namespace Pharmacie.Models;

/// <summary>Détail du billetage de clôture (sérialisé en JSON).</summary>
public class BilletageDetail
{
    public int Billet10000 { get; set; }
    public int Billet5000 { get; set; }
    public int Billet2000 { get; set; }
    public int Billet1000 { get; set; }
    public int Piece500 { get; set; }
    public int Piece250 { get; set; }
    public int Piece200 { get; set; }
    public int Piece100 { get; set; }
    public int Piece50 { get; set; }
    public int Piece25 { get; set; }
    public int Piece10 { get; set; }
    public int Piece5 { get; set; }

    public string? ModifiePar { get; set; }
    public DateTime? ModifieAt { get; set; }
    public string? RaisonModification { get; set; }

    public decimal Total =>
        Billet10000 * 10000m +
        Billet5000 * 5000m +
        Billet2000 * 2000m +
        Billet1000 * 1000m +
        Piece500 * 500m +
        Piece250 * 250m +
        Piece200 * 200m +
        Piece100 * 100m +
        Piece50 * 50m +
        Piece25 * 25m +
        Piece10 * 10m +
        Piece5 * 5m;
}
