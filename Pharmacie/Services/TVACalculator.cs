using Pharmacie.Models;

namespace Pharmacie.Services;

/// <summary>Calculs TVA pour déclaration DGID (Sénégal).</summary>
public static class TVACalculator
{
    public const decimal TauxStandard = 0.18m;
    public const decimal CoeffConsommable = 1.436m;
    public const decimal CoeffParapharmacie = 1.5m;

    /// <summary>
    /// Applique les champs dérivés (coefficient, TVA, prix de vente) selon le type de tarif.
    /// </summary>
    public static void AppliquerTarif(Product product)
    {
        switch (product.TarifType)
        {
            case TarifType.PrixPublicPPH:
                product.Coefficient = null;
                product.AssujettiTVA = false;
                product.TauxTVA = 0;
                break;

            case TarifType.Consommable:
                product.Coefficient = CoeffConsommable;
                product.AssujettiTVA = true;
                product.TauxTVA = 18;
                if (product.PurchasePrice > 0)
                    product.SalePrice = Math.Round(product.PurchasePrice * CoeffConsommable, 0);
                break;

            case TarifType.ParapharmSansTVA:
                product.Coefficient = CoeffParapharmacie;
                product.AssujettiTVA = false;
                product.TauxTVA = 0;
                if (product.PurchasePrice > 0)
                    product.SalePrice = Math.Round(product.PurchasePrice * CoeffParapharmacie, 0);
                break;

            case TarifType.ParapharmAvecTVA:
                product.Coefficient = CoeffParapharmacie;
                product.AssujettiTVA = true;
                product.TauxTVA = 18;
                if (product.PurchasePrice > 0)
                {
                    var ht = product.PurchasePrice * CoeffParapharmacie;
                    product.SalePrice = Math.Round(ht * (1 + TauxStandard), 0);
                }
                break;
        }
    }

    public static (decimal Exonere, decimal MontantHT, decimal MontantTVA, decimal MontantTTC)
        CalculerTVA(Product? product, decimal prixVente, int quantite)
    {
        var total = Math.Round(prixVente * quantite, 0);
        if (product == null)
            return (total, 0, 0, total);

        switch (product.TarifType)
        {
            case TarifType.PrixPublicPPH:
            case TarifType.ParapharmSansTVA:
                return (total, 0, 0, total);

            case TarifType.Consommable:
            {
                var ht = Math.Round(total / (1 + TauxStandard), 0);
                var tva = total - ht;
                return (0, ht, tva, total);
            }

            case TarifType.ParapharmAvecTVA:
            {
                var ht = Math.Round(product.PurchasePrice * CoeffParapharmacie * quantite, 0);
                var tva = Math.Round(ht * TauxStandard, 0);
                var ttc = ht + tva;
                return (0, ht, tva, ttc);
            }

            default:
                return (total, 0, 0, total);
        }
    }

    public static (decimal TotalExonere, decimal TotalHT, decimal TotalTVA, decimal TotalTTC)
        CalculerTVAJournee(IEnumerable<Sale> ventes)
    {
        decimal exonere = 0, ht = 0, tva = 0, ttc = 0;

        foreach (var vente in ventes)
        {
            foreach (var ligne in vente.Lines)
            {
                var (e, h, t, tc) = CalculerTVA(ligne.Product, ligne.UnitPrice, ligne.Quantity);
                exonere += e;
                ht += h;
                tva += t;
                ttc += tc;
            }
        }

        return (exonere, ht, tva, ttc);
    }
}
