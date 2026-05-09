using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class AdminUserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choisissez un rôle.")]
    [Display(Name = "Rôle")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Compte verrouillé (connexion impossible)")]
    public bool AccountLocked { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Si renseigné, au moins 6 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe (optionnel)")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    [Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string? ConfirmNewPassword { get; set; }
}
