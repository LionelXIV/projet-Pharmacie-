using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pharmacie.Authorization;

namespace Pharmacie.Data;

public static class IdentitySeed
{
    public const string AdminEmailConfigKey = "ADMIN_EMAIL";
    public const string AdminPasswordConfigKey = "ADMIN_PASSWORD";

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
    /// Crée un compte administrateur initial s'il n'existe pas, à partir de
    /// <see cref="AdminEmailConfigKey"/> et <see cref="AdminPasswordConfigKey"/> dans la configuration.
    /// </summary>
    public static async Task SeedInitialAdminIfMissingAsync(
        UserManager<IdentityUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        var email = configuration[AdminEmailConfigKey]?.Trim();
        var password = configuration[AdminPasswordConfigKey];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation(
                    "Compte administrateur initial non créé : définissez {EmailKey} et {PasswordKey} " +
                    "(variables d'environnement ou user-secrets) pour créer automatiquement un administrateur au démarrage.",
                    AdminEmailConfigKey,
                    AdminPasswordConfigKey);
            }
            else
            {
                logger.LogWarning(
                    "Compte administrateur initial non créé : {EmailKey} et/ou {PasswordKey} absents de la configuration. " +
                    "Créez un administrateur manuellement ou configurez ces variables avant le premier démarrage.",
                    AdminEmailConfigKey,
                    AdminPasswordConfigKey);
            }

            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                var msg = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Création du compte administrateur initial impossible : {msg}");
            }

            logger.LogInformation("Compte administrateur initial créé pour {Email}.", email);
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
