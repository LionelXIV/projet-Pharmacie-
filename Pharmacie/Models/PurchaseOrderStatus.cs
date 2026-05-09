using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum PurchaseOrderStatus
{
    [Display(Name = "Brouillon")]
    Brouillon = 0,

    [Display(Name = "Envoyée")]
    Envoyee = 1,

    [Display(Name = "Partiellement reçue")]
    PartiellementRecue = 2,

    [Display(Name = "Reçue")]
    Recue = 3,

    [Display(Name = "Annulée")]
    Annulee = 4
}
