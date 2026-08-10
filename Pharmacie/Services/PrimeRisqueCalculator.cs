using Pharmacie.Models;

namespace Pharmacie.Services;

public static class PrimeRisqueCalculator
{
    public const decimal PrimeTableau1 = 40m;
    public const decimal PrimeTableau2 = 30m;

    public static bool EstTableau1(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return false;

        var n = categoryName.ToUpperInvariant();
        // « TABLEAU II » contient « TABLEAU I » — exclure II d'abord
        if (n.Contains("TABLEAU II") || n.Contains("TABLEAU 2"))
            return false;

        return n.Contains("TABLEAU I") || n.Contains("TABLEAU 1");
    }

    public static bool EstTableau2(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return false;

        var n = categoryName.ToUpperInvariant();
        return n.Contains("TABLEAU II") || n.Contains("TABLEAU 2");
    }

    public static (
        int QteTableau1,
        int QteTableau2,
        decimal PrimeTableau1Total,
        decimal PrimeTableau2Total,
        decimal PrimeTotale)
        CalculerPrimes(IEnumerable<SaleLine> lignes)
    {
        var list = lignes as IList<SaleLine> ?? lignes.ToList();

        var qte1 = list
            .Where(l => EstTableau1(l.Product?.Category?.Name))
            .Sum(l => l.Quantity);

        var qte2 = list
            .Where(l => EstTableau2(l.Product?.Category?.Name))
            .Sum(l => l.Quantity);

        var prime1 = qte1 * PrimeTableau1;
        var prime2 = qte2 * PrimeTableau2;

        return (qte1, qte2, prime1, prime2, prime1 + prime2);
    }
}
