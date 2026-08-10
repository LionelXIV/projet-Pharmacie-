using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum TarifType
{
    [Display(Name = "Prix public (PPH) — Exonéré")]
    PrixPublicPPH = 0,

    [Display(Name = "Consommable (×1.436) — TVA incluse")]
    Consommable = 1,

    [Display(Name = "Parapharmacie sans TVA (×1.5)")]
    ParapharmSansTVA = 2,

    [Display(Name = "Parapharmacie avec TVA (×1.5 + 18%)")]
    ParapharmAvecTVA = 3,

    [Display(Name = "Prix public (PPH) avec TVA 18%")]
    PrixPublicPPHAvecTVA = 4
}
