using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.CanManageUsers)]
public class AdminUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly UserActivityReportService _activityReportService;
    private readonly ApplicationDbContext _context;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        UserActivityReportService activityReportService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _activityReportService = activityReportService;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var hideTitulaires = User.IsInRole(AppRoles.Pharmacien) && !AppRoles.IsTitulaire(User);
        var list = new List<AdminUserRowViewModel>();
        var users = await _userManager.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .ToListAsync();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (hideTitulaires && AppRoles.HasTitulaireRole(roles))
                continue;

            var locked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
            var isTitulaire = AppRoles.HasTitulaireRole(roles);
            var roleLabels = roles
                .Select(AppRoles.GetRoleLabel)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            list.Add(new AdminUserRowViewModel
            {
                Id = u.Id,
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName)
                    ? (u.Email ?? u.UserName ?? u.Id)
                    : u.DisplayName,
                Email = u.Email ?? "—",
                RoleLabels = roleLabels,
                RolesDisplay = string.Join(", ", roleLabels),
                LoginType = isTitulaire || string.IsNullOrEmpty(u.PinHash) ? "Email" : "PIN",
                IsLockedOut = locked,
                IsTitulaire = isTitulaire
            });
        }

        return View(list);
    }

    public IActionResult Create()
    {
        ViewBag.Roles = BuildRoleChoices(User, null);
        return View(new AdminUserCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateViewModel model)
    {
        model.RolesSelectionnes ??= new List<string>();
        var roles = model.RolesSelectionnes
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        model.RolesSelectionnes = roles;

        if (roles.Count == 0)
            ModelState.AddModelError(nameof(model.RolesSelectionnes), "Au moins un rôle est obligatoire.");
        else if (roles.Any(r => !AppRoles.AllAssignableRoles.Contains(r)))
            ModelState.AddModelError(nameof(model.RolesSelectionnes), "Rôle invalide.");

        if (!AppRoles.IsTitulaire(User) && roles.Contains(AppRoles.PharmacienTitulaire))
            ModelState.AddModelError(nameof(model.RolesSelectionnes), "Vous ne pouvez pas attribuer le rôle Pharmacien Titulaire.");

        var isTitulaireAccount = roles.Contains(AppRoles.PharmacienTitulaire);
        ValidateCreateModel(model, isTitulaireAccount);

        if (ModelState.IsValid)
        {
            IdentityResult create;
            ApplicationUser user;

            if (isTitulaireAccount)
            {
                var displayName = model.DisplayName!.Trim();
                if (await DisplayNameExistsAsync(displayName))
                {
                    ModelState.AddModelError(nameof(model.DisplayName), "Cet identifiant affiché est déjà utilisé.");
                    ViewBag.Roles = BuildRoleChoices(User, roles);
                    return View(model);
                }

                user = new ApplicationUser
                {
                    UserName = model.Email!.Trim(),
                    Email = model.Email!.Trim(),
                    EmailConfirmed = true,
                    DisplayName = displayName,
                    PinHash = null
                };
                create = await _userManager.CreateAsync(user, model.Password!);
            }
            else
            {
                var displayName = model.DisplayName!.Trim();
                if (await DisplayNameExistsAsync(displayName))
                {
                    ModelState.AddModelError(nameof(model.DisplayName), "Cet identifiant est déjà utilisé.");
                    ViewBag.Roles = BuildRoleChoices(User, roles);
                    return View(model);
                }

                var email = await BuildInternalEmailAsync(displayName);
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = displayName
                };
                user.PinHash = _passwordHasher.HashPassword(user, model.Pin!);
                create = await _userManager.CreateAsync(user, GenerateInternalPassword());
            }

            if (create.Succeeded)
            {
                foreach (var role in roles)
                {
                    var addRole = await _userManager.AddToRoleAsync(user, role);
                    if (!addRole.Succeeded)
                    {
                        foreach (var e in addRole.Errors)
                            ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
                        ViewBag.Roles = BuildRoleChoices(User, roles);
                        return View(model);
                    }
                }

                var labels = string.Join(", ", roles.Select(AppRoles.GetRoleLabel));
                TempData["Success"] = $"Utilisateur « {user.DisplayName} » créé avec le(s) rôle(s) {labels}.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var e in create.Errors)
                ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
        }

        ViewBag.Roles = BuildRoleChoices(User, roles);
        return View(model);
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        if (AppRoles.HasTitulaireRole(roles) && !AppRoles.IsTitulaire(User))
            return Forbid();

        var rolesSelectionnes = roles
            .Select(MapToAssignableRole)
            .Where(r => AppRoles.AllAssignableRoles.Contains(r))
            .Distinct()
            .ToList();

        var locked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        var isTitulaireAccount = AppRoles.HasTitulaireRole(roles);

        var vm = new AdminUserEditViewModel
        {
            Id = user.Id,
            Email = user.Email ?? user.UserName ?? user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            RolesSelectionnes = rolesSelectionnes,
            AccountLocked = locked,
            IsTitulaireAccount = isTitulaireAccount,
            IsPinLogin = !isTitulaireAccount && !string.IsNullOrEmpty(user.PinHash)
        };

        ViewBag.Roles = BuildRoleChoices(User, rolesSelectionnes);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserEditViewModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        model.RolesSelectionnes ??= new List<string>();
        var roles = model.RolesSelectionnes
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        model.RolesSelectionnes = roles;

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
            return NotFound();

        var existingRoles = await _userManager.GetRolesAsync(user);
        if (AppRoles.HasTitulaireRole(existingRoles) && !AppRoles.IsTitulaire(User))
            return Forbid();

        if (roles.Count == 0)
            ModelState.AddModelError(nameof(model.RolesSelectionnes), "Au moins un rôle est obligatoire.");
        else if (roles.Any(r => !AppRoles.AllAssignableRoles.Contains(r)))
            ModelState.AddModelError(nameof(model.RolesSelectionnes), "Rôle invalide.");

        if (!AppRoles.IsTitulaire(User) && roles.Contains(AppRoles.PharmacienTitulaire))
            ModelState.AddModelError(nameof(model.RolesSelectionnes), "Vous ne pouvez pas attribuer le rôle Pharmacien Titulaire.");

        var isTitulaireAccount = roles.Contains(AppRoles.PharmacienTitulaire);
        model.IsTitulaireAccount = isTitulaireAccount;
        model.IsPinLogin = !isTitulaireAccount;

        if (isTitulaireAccount)
        {
            if (!string.IsNullOrWhiteSpace(model.NewPassword) || !string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
            {
                if (string.IsNullOrWhiteSpace(model.NewPassword) || string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                    ModelState.AddModelError(string.Empty, "Renseignez le nouveau mot de passe et sa confirmation, ou laissez les deux vides.");
                else if (model.NewPassword != model.ConfirmNewPassword)
                    ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Les mots de passe ne correspondent pas.");
                else if (model.NewPassword!.Length < 6)
                    ModelState.AddModelError(nameof(model.NewPassword), "Le mot de passe doit contenir au moins 6 caractères.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.NewPin) || !string.IsNullOrWhiteSpace(model.ConfirmNewPin))
        {
            if (string.IsNullOrWhiteSpace(model.NewPin) || string.IsNullOrWhiteSpace(model.ConfirmNewPin))
                ModelState.AddModelError(string.Empty, "Renseignez le nouveau code PIN et sa confirmation, ou laissez les deux vides.");
            else if (model.NewPin != model.ConfirmNewPin)
                ModelState.AddModelError(nameof(model.ConfirmNewPin), "Les codes PIN ne correspondent pas.");
            else if (!Regex.IsMatch(model.NewPin!, @"^\d{4}$"))
                ModelState.AddModelError(nameof(model.NewPin), "Le code PIN doit contenir exactement 4 chiffres.");
        }

        if (model.Id == currentUserId)
        {
            if (model.AccountLocked)
                ModelState.AddModelError(nameof(model.AccountLocked), "Vous ne pouvez pas verrouiller votre propre compte.");
            if (AppRoles.IsTitulaire(User) && !roles.Contains(AppRoles.PharmacienTitulaire))
                ModelState.AddModelError(nameof(model.RolesSelectionnes), "Vous ne pouvez pas retirer votre propre rôle Pharmacien Titulaire.");
        }

        var displayName = model.DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName))
            ModelState.AddModelError(nameof(model.DisplayName), "L'identifiant affiché est obligatoire.");
        else if (await DisplayNameExistsAsync(displayName, excludeUserId: user.Id))
            ModelState.AddModelError(nameof(model.DisplayName), "Cet identifiant affiché est déjà utilisé.");

        if (!ModelState.IsValid)
        {
            ViewBag.Roles = BuildRoleChoices(User, roles);
            return View(model);
        }

        user.DisplayName = displayName;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors)
                ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
            ViewBag.Roles = BuildRoleChoices(User, roles);
            return View(model);
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Count > 0)
        {
            var remove = await _userManager.RemoveFromRolesAsync(user, userRoles);
            if (!remove.Succeeded)
            {
                foreach (var e in remove.Errors)
                    ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
                ViewBag.Roles = BuildRoleChoices(User, roles);
                return View(model);
            }
        }

        foreach (var role in roles)
        {
            var add = await _userManager.AddToRoleAsync(user, role);
            if (!add.Succeeded)
            {
                foreach (var e in add.Errors)
                    ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
                ViewBag.Roles = BuildRoleChoices(User, roles);
                return View(model);
            }
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        if (model.AccountLocked)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        if (isTitulaireAccount)
        {
            user.PinHash = null;
            await _userManager.UpdateAsync(user);

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwd = await _userManager.ResetPasswordAsync(user, token, model.NewPassword!);
                if (!pwd.Succeeded)
                {
                    foreach (var e in pwd.Errors)
                        ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
                    model.IsPinLogin = false;
                    ViewBag.Roles = BuildRoleChoices(User, roles);
                    return View(model);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.NewPin))
        {
            user.PinHash = _passwordHasher.HashPassword(user, model.NewPin!);
            await _userManager.UpdateAsync(user);
        }

        TempData["Success"] = $"Utilisateur « {user.DisplayName} » mis à jour.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DeleteWithReport(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == currentUserId)
        {
            TempData["Error"] = "Vous ne pouvez pas supprimer votre propre compte.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        if (string.IsNullOrEmpty(currentUserId))
            return Challenge();

        var preview = await _activityReportService.GenerateReportAsync(id, currentUserId);
        return View(new DeleteWithReportViewModel
        {
            UserId = id,
            Preview = preview
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("DeleteWithReport")]
    public async Task<IActionResult> DeleteWithReportConfirmed(string id, bool confirmed)
    {
        if (!confirmed)
            return RedirectToAction(nameof(Index));

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
            return Challenge();

        if (id == currentUserId)
        {
            TempData["Error"] = "Vous ne pouvez pas supprimer votre propre compte.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var report = await _activityReportService.GenerateReportAsync(id, currentUserId);
        _context.UserActivityReports.Add(report);
        await _context.SaveChangesAsync();

        var imports = await _context.ImportBatches
            .Where(b => b.UploadedByUserId == id || b.ConfirmedByUserId == id)
            .ToListAsync();
        foreach (var batch in imports)
        {
            if (batch.UploadedByUserId == id)
                batch.UploadedByUserId = null;
            if (batch.ConfirmedByUserId == id)
                batch.ConfirmedByUserId = null;
        }

        if (imports.Count > 0)
            await _context.SaveChangesAsync();

        var delete = await _userManager.DeleteAsync(user);
        if (!delete.Succeeded)
        {
            TempData["Error"] = "Le rapport a été archivé, mais la suppression du compte a échoué : "
                + string.Join("; ", delete.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(ActivityReports));
        }

        TempData["Success"] = "Utilisateur supprimé. Le rapport d'activité a été archivé.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ActivityReports()
    {
        var list = await _context.UserActivityReports
            .AsNoTracking()
            .OrderByDescending(r => r.DeletedAt)
            .ToListAsync();
        return View("ActivityReports/Index", list);
    }

    public async Task<IActionResult> ActivityReportDetails(int id)
    {
        var report = await _context.UserActivityReports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (report == null)
            return NotFound();

        ViewBag.ReportData = UserActivityReportService.Deserialize(report.ActivityReportJson);
        return View("ActivityReports/Details", report);
    }

    public async Task<IActionResult> ExportActivityReportCsv(int reportId)
    {
        var report = await _context.UserActivityReports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId);
        if (report == null)
            return NotFound();

        var data = UserActivityReportService.Deserialize(report.ActivityReportJson);
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Section"),
            ReportCsvFormatter.Escape("Id"),
            ReportCsvFormatter.Escape("Date"),
            ReportCsvFormatter.Escape("Détail"),
            ReportCsvFormatter.Escape("Quantité"),
            ReportCsvFormatter.Escape("Montant"),
            ReportCsvFormatter.Escape("Notes")));

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Utilisateur"),
            "",
            ReportCsvFormatter.Escape(report.DeletedAt.ToLocalTime().ToString("g")),
            ReportCsvFormatter.Escape($"{report.DeletedUserDisplayName} | {report.DeletedUserEmail} | {report.DeletedUserRole} | {report.DeletedUserConnectionType}"),
            "",
            "",
            ReportCsvFormatter.Escape($"Supprimé par {report.DeletedByDisplayName}")));

        if (data?.Sales != null)
        {
            foreach (var s in data.Sales)
            {
                var products = string.Join(", ", s.Products.Select(p => $"{p.CommercialName} x{p.Quantity}"));
                sb.AppendLine(ReportCsvFormatter.Join(
                    ReportCsvFormatter.Escape("Vente"),
                    ReportCsvFormatter.IntInvariant(s.Id),
                    ReportCsvFormatter.Escape(s.SoldAt.ToLocalTime().ToString("g")),
                    ReportCsvFormatter.Escape($"{s.PaymentMethod} — {products}"),
                    "",
                    ReportCsvFormatter.FcfaCsvAmount(s.Total),
                    ""));
            }
        }

        if (data?.Movements != null)
        {
            foreach (var m in data.Movements)
            {
                sb.AppendLine(ReportCsvFormatter.Join(
                    ReportCsvFormatter.Escape("Mouvement"),
                    ReportCsvFormatter.IntInvariant(m.Id),
                    ReportCsvFormatter.Escape(m.OccurredAt.ToLocalTime().ToString("g")),
                    ReportCsvFormatter.Escape($"{m.Type} — {m.Product}" + (string.IsNullOrEmpty(m.LotNumber) ? "" : $" (lot {m.LotNumber})")),
                    ReportCsvFormatter.IntInvariant(m.Quantity),
                    "",
                    ReportCsvFormatter.Escape(m.Reason)));
            }
        }

        if (data?.Imports != null)
        {
            foreach (var i in data.Imports)
            {
                sb.AppendLine(ReportCsvFormatter.Join(
                    ReportCsvFormatter.Escape("Import"),
                    ReportCsvFormatter.IntInvariant(i.Id),
                    ReportCsvFormatter.Escape(i.UploadedAt.ToLocalTime().ToString("g")),
                    ReportCsvFormatter.Escape($"{i.FileName} — {i.Role}"),
                    "",
                    "",
                    ReportCsvFormatter.Escape(i.Status)));
            }
        }

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Résumé"),
            "",
            "",
            ReportCsvFormatter.Escape($"{report.TotalSales} ventes, {report.TotalStockMovements} mouvements"),
            "",
            ReportCsvFormatter.FcfaCsvAmount(report.TotalSalesAmount),
            ""));

        var slug = "rapport_utilisateur_" + Regex.Replace(report.DeletedUserDisplayName, @"[^a-zA-Z0-9_-]+", "_");
        return ReportCsvFormatter.FileResult(this, sb.ToString(), slug);
    }

    private static List<RoleChoixViewModel> BuildRoleChoices(ClaimsPrincipal current, IEnumerable<string>? selected)
    {
        var selectedSet = new HashSet<string>(
            (selected ?? Enumerable.Empty<string>()).Select(MapToAssignableRole),
            StringComparer.Ordinal);

        var roles = new List<RoleChoixViewModel>
        {
            new()
            {
                Nom = AppRoles.Pharmacien,
                Label = AppRoles.GetRoleLabel(AppRoles.Pharmacien),
                Description = "Accès complet sauf finances",
                Selectionne = selectedSet.Contains(AppRoles.Pharmacien)
            },
            new()
            {
                Nom = AppRoles.Vendeur,
                Label = AppRoles.GetRoleLabel(AppRoles.Vendeur),
                Description = "Gestion stock et catalogue",
                Selectionne = selectedSet.Contains(AppRoles.Vendeur)
            },
            new()
            {
                Nom = AppRoles.Caissier,
                Label = AppRoles.GetRoleLabel(AppRoles.Caissier),
                Description = "Ventes et caisse",
                Selectionne = selectedSet.Contains(AppRoles.Caissier)
            },
            new()
            {
                Nom = AppRoles.AssistantPharmacien,
                Label = AppRoles.GetRoleLabel(AppRoles.AssistantPharmacien),
                Description = "Ventes et patients",
                Selectionne = selectedSet.Contains(AppRoles.AssistantPharmacien)
            },
            new()
            {
                Nom = AppRoles.Stagiaire,
                Label = AppRoles.GetRoleLabel(AppRoles.Stagiaire),
                Description = "Catalogue lecture + réception BL",
                Selectionne = selectedSet.Contains(AppRoles.Stagiaire)
            },
            new()
            {
                Nom = AppRoles.Comptable,
                Label = AppRoles.GetRoleLabel(AppRoles.Comptable),
                Description = "Accès Finance et État du stock uniquement",
                Selectionne = selectedSet.Contains(AppRoles.Comptable)
            }
        };

        if (AppRoles.IsTitulaire(current) || selectedSet.Contains(AppRoles.PharmacienTitulaire))
        {
            roles.Insert(0, new RoleChoixViewModel
            {
                Nom = AppRoles.PharmacienTitulaire,
                Label = AppRoles.GetRoleLabel(AppRoles.PharmacienTitulaire),
                Description = "Accès complet",
                Selectionne = selectedSet.Contains(AppRoles.PharmacienTitulaire)
            });
        }

        return roles;
    }

    private static string MapToAssignableRole(string role) => role switch
    {
        AppRoles.Administrateur => AppRoles.PharmacienTitulaire,
        AppRoles.Assistant => AppRoles.AssistantPharmacien,
        AppRoles.GestionnaireStock => AppRoles.Vendeur,
        _ => role
    };

    private void ValidateCreateModel(AdminUserCreateViewModel model, bool isTitulaireAccount)
    {
        if (isTitulaireAccount)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), "L'adresse email est obligatoire.");
            if (string.IsNullOrWhiteSpace(model.DisplayName))
                ModelState.AddModelError(nameof(model.DisplayName), "L'identifiant affiché est obligatoire.");
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Le mot de passe est obligatoire.");
            if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
                ModelState.AddModelError(nameof(model.ConfirmPassword), "La confirmation est obligatoire.");
            else if (model.Password != model.ConfirmPassword)
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Les mots de passe ne correspondent pas.");
        }
        else if (model.RolesSelectionnes.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(model.DisplayName))
                ModelState.AddModelError(nameof(model.DisplayName), "L'identifiant est obligatoire.");
            if (string.IsNullOrWhiteSpace(model.Pin))
                ModelState.AddModelError(nameof(model.Pin), "Le code PIN est obligatoire.");
            else if (!Regex.IsMatch(model.Pin, @"^\d{4}$"))
                ModelState.AddModelError(nameof(model.Pin), "Le code PIN doit contenir exactement 4 chiffres.");
            if (string.IsNullOrWhiteSpace(model.ConfirmPin))
                ModelState.AddModelError(nameof(model.ConfirmPin), "La confirmation du PIN est obligatoire.");
            else if (model.Pin != model.ConfirmPin)
                ModelState.AddModelError(nameof(model.ConfirmPin), "Les codes PIN ne correspondent pas.");
        }
    }

    private async Task<bool> DisplayNameExistsAsync(string displayName, string? excludeUserId = null)
    {
        var normalized = displayName.Trim().ToUpperInvariant();
        return await _userManager.Users.AnyAsync(u =>
            u.DisplayName.ToUpper() == normalized
            && (excludeUserId == null || u.Id != excludeUserId));
    }

    private async Task<string> BuildInternalEmailAsync(string displayName)
    {
        var local = Regex.Replace(displayName.Trim().ToLowerInvariant(), @"[^a-z0-9]", "");
        if (string.IsNullOrEmpty(local))
            local = "user";

        var baseEmail = $"{local}@pharmacie.local";
        var email = baseEmail;
        var n = 1;
        while (await _userManager.FindByEmailAsync(email) != null)
        {
            email = $"{local}{n}@pharmacie.local";
            n++;
        }

        return email;
    }

    private static string GenerateInternalPassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', 'A')
            .Replace('/', 'B')
            .Replace('=', 'C');
        return $"Aa1!{token}";
    }
}
