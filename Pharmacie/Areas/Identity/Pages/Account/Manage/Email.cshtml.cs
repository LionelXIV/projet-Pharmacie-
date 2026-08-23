using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pharmacie.Models;

namespace Pharmacie.Areas.Identity.Pages.Account.Manage;

public class EmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EmailModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public string Email { get; set; } = string.Empty;

    public bool IsEmailConfirmed { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Impossible de charger l'utilisateur avec l'ID '{_userManager.GetUserId(User)}'.");

        Email = await _userManager.GetEmailAsync(user) ?? string.Empty;
        IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        return Page();
    }
}
