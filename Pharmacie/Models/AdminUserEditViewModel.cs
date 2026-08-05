using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class AdminUserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Display(Name = "Adresse email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'identifiant affiché est obligatoire.")]
    [Display(Name = "Identifiant affiché")]
    [StringLength(100, ErrorMessage = "L'identifiant ne peut pas dépasser 100 caractères.")]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Rôles")]
    public List<string> RolesSelectionnes { get; set; } = new();

    public bool IsTitulaireAccount { get; set; }

    public bool IsPinLogin { get; set; }

    [Display(Name = "Compte verrouillé (connexion impossible)")]
    public bool AccountLocked { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Si renseigné, au moins 6 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe (optionnel)")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    public string? ConfirmNewPassword { get; set; }

    [Display(Name = "Nouveau code PIN (optionnel)")]
    [RegularExpression(@"^(\d{4})?$", ErrorMessage = "Le code PIN doit contenir exactement 4 chiffres.")]
    public string? NewPin { get; set; }

    [Display(Name = "Confirmer le code PIN")]
    public string? ConfirmNewPin { get; set; }
}
