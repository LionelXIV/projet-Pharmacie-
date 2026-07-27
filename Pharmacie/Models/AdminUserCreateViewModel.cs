using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class AdminUserCreateViewModel
{
    [Required(ErrorMessage = "Choisissez un rôle.")]
    [Display(Name = "Rôle")]
    public string Role { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Adresse email invalide.")]
    [Display(Name = "Adresse email")]
    public string? Email { get; set; }

    [Display(Name = "Identifiant affiché")]
    [StringLength(100, ErrorMessage = "L'identifiant ne peut pas dépasser 100 caractères.")]
    public string? DisplayName { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le mot de passe")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "Code PIN")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Le code PIN doit contenir exactement 4 chiffres.")]
    public string? Pin { get; set; }

    [Display(Name = "Confirmer le code PIN")]
    public string? ConfirmPin { get; set; }
}
