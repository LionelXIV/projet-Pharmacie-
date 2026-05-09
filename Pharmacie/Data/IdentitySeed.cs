using Microsoft.AspNetCore.Identity;
using Pharmacie.Authorization;

namespace Pharmacie.Data;

public static class IdentitySeed
{
    /// <summary>Compte admin local — utilisé uniquement par <see cref="SeedDevAdminIfMissingAsync"/> en environnement Development.</summary>
    public const string DevAdminEmail = "admin@pharmacie.local";

    /// <summary>Mot de passe de développement uniquement. Ne jamais réutiliser en production.</summary>
    public const string DevAdminPassword = "Admin123!";

    public static readonly string[] RoleNames = AppRoles.AllAssignableRoles;

    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in RoleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    /// <summary>
    /// Crée <see cref="DevAdminEmail"/> avec le rôle Administrateur s'il n'existe pas.
    /// À appeler uniquement en développement local (pas de mot de passe faible en prod).
    /// </summary>
    public static async Task SeedDevAdminIfMissingAsync(UserManager<IdentityUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(DevAdminEmail);
        if (user == null)
        {
            user = new IdentityUser
            {
                UserName = DevAdminEmail,
                Email = DevAdminEmail,
                EmailConfirmed = true
            };
            var create = await userManager.CreateAsync(user, DevAdminPassword);
            if (!create.Succeeded)
            {
                var msg = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Création du compte admin de développement impossible : {msg}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Administrateur))
        {
            var add = await userManager.AddToRoleAsync(user, AppRoles.Administrateur);
            if (!add.Succeeded)
            {
                var msg = string.Join("; ", add.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Attribution du rôle Administrateur impossible : {msg}");
            }
        }
    }
}
