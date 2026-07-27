using Microsoft.AspNetCore.Identity;

namespace Pharmacie.Models;

public class ApplicationUser : IdentityUser
{
    /// <summary>Identifiant affiché dans l'historique (Admin choisi à la création ; autres = pseudo).</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Hash du PIN pour connexion rapide. Null pour les administrateurs.</summary>
    public string? PinHash { get; set; }
}
