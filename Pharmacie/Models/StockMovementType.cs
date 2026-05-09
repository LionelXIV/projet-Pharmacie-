namespace Pharmacie.Models;

public enum StockMovementType
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Entrée")]
    Entree = 0,

    [System.ComponentModel.DataAnnotations.Display(Name = "Sortie")]
    Sortie = 1,

    [System.ComponentModel.DataAnnotations.Display(Name = "Ajustement")]
    Ajustement = 2
}
