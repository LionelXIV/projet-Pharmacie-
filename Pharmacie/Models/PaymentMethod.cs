using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum PaymentMethod
{
    [Display(Name = "Espèces")]
    Especes = 0,

    [Display(Name = "Wave")]
    Wave = 1,

    [Display(Name = "Orange Money")]
    OrangeMoney = 2,

    [Display(Name = "Chèque")]
    Cheque = 3,

    [Display(Name = "Virement")]
    Virement = 4,

    [Display(Name = "TPE/Carte")]
    TPE = 5,

    [Display(Name = "Yas Money")]
    YasMoney = 6,

    [Display(Name = "Autre")]
    Autre = 7,
}
