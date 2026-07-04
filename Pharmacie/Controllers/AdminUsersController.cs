using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Administrateur)]
public class AdminUsersController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;

    public AdminUsersController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var list = new List<AdminUserRowViewModel>();
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var locked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
            list.Add(new AdminUserRowViewModel
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? u.Id,
                RolesDisplay = string.Join(", ", roles.OrderBy(r => r)),
                IsLockedOut = locked
            });
        }

        return View(list);
    }

    public IActionResult Create()
    {
        var vm = new AdminUserCreateViewModel();
        PopulateRoleSelect(vm.Role);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateViewModel model)
    {
        if (!AppRoles.AllAssignableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Rôle invalide.");

        if (ModelState.IsValid)
        {
            var user = new IdentityUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = true
            };
            var create = await _userManager.CreateAsync(user, model.Password);
            if (create.Succeeded)
            {
                var addRole = await _userManager.AddToRoleAsync(user, model.Role);
                if (addRole.Succeeded)
                {
                    TempData["Success"] = $"Utilisateur « {user.Email} » créé avec le rôle {model.Role}.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var e in addRole.Errors)
                    ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
            }
            else
            {
                foreach (var e in create.Errors)
                    ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
            }
        }

        PopulateRoleSelect(model.Role);
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
        var currentRole = AppRoles.AllAssignableRoles.FirstOrDefault(r => roles.Contains(r))
            ?? roles.OrderBy(r => r).FirstOrDefault()
            ?? AppRoles.Assistant;

        var locked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        var vm = new AdminUserEditViewModel
        {
            Id = user.Id,
            Email = user.Email ?? user.UserName ?? user.Id,
            Role = AppRoles.AllAssignableRoles.Contains(currentRole) ? currentRole : AppRoles.Assistant,
            AccountLocked = locked
        };

        PopulateRoleSelect(vm.Role);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserEditViewModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!AppRoles.AllAssignableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Rôle invalide.");

        if (!string.IsNullOrWhiteSpace(model.NewPassword) || !string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
        {
            if (string.IsNullOrWhiteSpace(model.NewPassword) || string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                ModelState.AddModelError(string.Empty, "Renseignez le nouveau mot de passe et sa confirmation, ou laissez les deux vides.");
            else if (model.NewPassword != model.ConfirmNewPassword)
                ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Les mots de passe ne correspondent pas.");
            else if (model.NewPassword!.Length < 6)
                ModelState.AddModelError(nameof(model.NewPassword), "Le mot de passe doit contenir au moins 6 caractères.");
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
            return NotFound();

        if (model.Id == currentUserId)
        {
            if (model.AccountLocked)
                ModelState.AddModelError(nameof(model.AccountLocked), "Vous ne pouvez pas verrouiller votre propre compte.");
            if (model.Role != AppRoles.Administrateur)
                ModelState.AddModelError(nameof(model.Role), "Vous ne pouvez pas retirer votre propre rôle Administrateur.");
        }

        if (!ModelState.IsValid)
        {
            PopulateRoleSelect(model.Role);
            return View(model);
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var remove = await _userManager.RemoveFromRolesAsync(user, userRoles);
        if (!remove.Succeeded)
        {
            foreach (var e in remove.Errors)
                ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
            PopulateRoleSelect(model.Role);
            return View(model);
        }

        var add = await _userManager.AddToRoleAsync(user, model.Role);
        if (!add.Succeeded)
        {
            foreach (var e in add.Errors)
                ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
            PopulateRoleSelect(model.Role);
            return View(model);
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        if (model.AccountLocked)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwd = await _userManager.ResetPasswordAsync(user, token, model.NewPassword!);
            if (!pwd.Succeeded)
            {
                foreach (var e in pwd.Errors)
                    ModelState.AddModelError(string.Empty, $"{e.Code}: {e.Description}");
                PopulateRoleSelect(model.Role);
                return View(model);
            }
        }

        TempData["Success"] = $"Utilisateur « {user.Email} » mis à jour.";
        return RedirectToAction(nameof(Index));
    }

    private void PopulateRoleSelect(string? selected)
    {
        var items = AppRoles.AllAssignableRoles.Select(r => new SelectListItem
        {
            Value = r,
            Text = r
        }).ToList();
        ViewBag.Role = new SelectList(items, "Value", "Text", selected);
    }
}
