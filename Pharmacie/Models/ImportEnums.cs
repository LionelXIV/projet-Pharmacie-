using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public enum ImportBatchStatus
{
    [Display(Name = "En attente de validation")]
    EnAttenteValidation = 0,

    [Display(Name = "Confirmé")]
    Confirme = 1,

    [Display(Name = "Annulé")]
    Annule = 2
}

public enum ImportLineAction
{
    [Display(Name = "Création produit")]
    CreationProduit = 0,

    [Display(Name = "Mise à jour prix")]
    MiseAJourPrix = 1,

    [Display(Name = "Nouveau lot")]
    NouveauLot = 2,

    [Display(Name = "Ignorée")]
    Ignoree = 3,

    [Display(Name = "Anomalie")]
    Anomalie = 4
}

public enum ImportAnomalyType
{
    [Display(Name = "PPH zéro ou inférieur au prix fab.")]
    PphZeroOuInferieurAuPrixFab = 0,

    [Display(Name = "Quantité négative")]
    QuantiteNegative = 1,

    [Display(Name = "CIP dupliqué dans le fichier")]
    CipDupliqueDansLeFichier = 2,

    [Display(Name = "CIP manquant ou invalide")]
    CipManquantOuInvalide = 3,

    [Display(Name = "Libellé vide")]
    LibelleVide = 4,

    [Display(Name = "CIP identique, libellé différent")]
    CipIdentiqueLibelleDifferent = 5
}

public enum ImportAnomalySeverity
{
    [Display(Name = "Bloquante")]
    Bloquante = 0,

    [Display(Name = "Avertissement")]
    Avertissement = 1
}
