using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class AdminUserCreateViewModel
{
    [Required(ErrorMessage = "L’email est obligatoire.")]
    [EmailAddress]
    [Display(Name = "Email (identifiant)")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Le mot de passe doit contenir au moins 6 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation est obligatoire.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le mot de passe")]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choisissez un rôle.")]
    [Display(Name = "Rôle")]
    public string Role { get; set; } = string.Empty;
}
