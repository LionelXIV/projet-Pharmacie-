using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum PaymentMethod
{
    [Display(Name = "Espèces")]
    Especes = 0,

    [Display(Name = "Wave")]
    Wave = 1,

    [Display(Name = "Orange Money")]
    OrangeMoney = 2
}
