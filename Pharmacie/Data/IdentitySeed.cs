using Microsoft.AspNetCore.Identity;
using Pharmacie.Models;
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

        // Anciens rôles conservés pendant la transition (ne pas supprimer automatiquement).
        foreach (var legacy in AppRoles.LegacyRoles)
        {
            if (!await roleManager.RoleExistsAsync(legacy))
                await roleManager.CreateAsync(new IdentityRole(legacy));
        }
    }

    /// <summary>
    /// Migre les attributions legacy vers les nouveaux rôles sans retirer les anciens.
    /// </summary>
    public static async Task MigrateLegacyRoleAssignmentsAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        var users = userManager.Users.ToList();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.Administrateur)
                && !roles.Contains(AppRoles.PharmacienTitulaire))
            {
                await userManager.AddToRoleAsync(user, AppRoles.PharmacienTitulaire);
                logger.LogInformation("Rôle PharmacienTitulaire ajouté pour {User}.", user.Email);
            }

            if (roles.Contains(AppRoles.Assistant)
                && !roles.Contains(AppRoles.AssistantPharmacien))
            {
                await userManager.AddToRoleAsync(user, AppRoles.AssistantPharmacien);
                logger.LogInformation("Rôle AssistantPharmacien ajouté pour {User}.", user.Email);
            }

            if (roles.Contains(AppRoles.GestionnaireStock)
                && !roles.Contains(AppRoles.Vendeur))
            {
                await userManager.AddToRoleAsync(user, AppRoles.Vendeur);
                logger.LogInformation("Rôle Vendeur ajouté pour {User}.", user.Email);
            }
        }
    }

    /// <summary>
    /// Crée un compte Pharmacien Titulaire initial s'il n'existe pas.
    /// </summary>
    public static async Task SeedInitialAdminIfMissingAsync(
        UserManager<ApplicationUser> userManager,
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
                    "Compte Pharmacien Titulaire initial non créé : définissez {EmailKey} et {PasswordKey} " +
                    "(variables d'environnement ou user-secrets) pour créer automatiquement un titulaire au démarrage.",
                    AdminEmailConfigKey,
                    AdminPasswordConfigKey);
            }
            else
            {
                logger.LogWarning(
                    "Compte Pharmacien Titulaire initial non créé : {EmailKey} et/ou {PasswordKey} absents de la configuration. " +
                    "Créez un titulaire manuellement ou configurez ces variables avant le premier démarrage.",
                    AdminEmailConfigKey,
                    AdminPasswordConfigKey);
            }

            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = email.Contains('@') ? email[..email.IndexOf('@')] : email,
                PinHash = null
            };
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                var msg = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Création du compte Pharmacien Titulaire initial impossible : {msg}");
            }

            logger.LogInformation("Compte Pharmacien Titulaire initial créé pour {Email}.", email);
        }
        else if (string.IsNullOrWhiteSpace(user.DisplayName))
        {
            user.DisplayName = email.Contains('@') ? email[..email.IndexOf('@')] : email;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.PharmacienTitulaire))
        {
            var add = await userManager.AddToRoleAsync(user, AppRoles.PharmacienTitulaire);
            if (!add.Succeeded)
            {
                var msg = string.Join("; ", add.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Attribution du rôle PharmacienTitulaire impossible : {msg}");
            }
        }
    }
}
