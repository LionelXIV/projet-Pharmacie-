using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum ProductType
{
    [Display(Name = "Inconnu")]
    Inconnu = 0,

    [Display(Name = "Médicament")]
    Medicament = 1,

    [Display(Name = "Parapharmacie")]
    Parapharmacie = 2
}
