using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Models;

namespace Pharmacie.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public QuickInputModel QuickInput { get; set; } = new();

    public string ActiveTab { get; set; } = "admin";

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Display(Name = "Adresse email")]
        [EmailAddress(ErrorMessage = "Adresse email invalide.")]
        public string? Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe")]
        public string? Password { get; set; }

        [Display(Name = "Se souvenir de moi")]
        public bool RememberMe { get; set; }
    }

    public class QuickInputModel
    {
        [Display(Name = "Identifiant")]
        public string? DisplayName { get; set; }

        [Display(Name = "Code PIN")]
        [DataType(DataType.Password)]
        public string? Pin { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        ReturnUrl = returnUrl;
        ActiveTab = "admin";
    }

    public async Task<IActionResult> OnPostAdminAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;
        ActiveTab = "admin";
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (string.IsNullOrWhiteSpace(Input.Email))
            ModelState.AddModelError("Input.Email", "L'adresse email est requise.");
        if (string.IsNullOrWhiteSpace(Input.Password))
            ModelState.AddModelError("Input.Password", "Le mot de passe est requis.");

        if (!ModelState.IsValid)
            return Page();

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email!, Input.Password!, isPersistent: false, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString("O"));
            _logger.LogInformation("User logged in (admin).");
            return LocalRedirect(returnUrl);
        }
        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = false });
        }
        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "Identifiants incorrects");
        return Page();
    }

    public async Task<IActionResult> OnPostQuickAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;
        ActiveTab = "quick";
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (string.IsNullOrWhiteSpace(QuickInput.DisplayName))
            ModelState.AddModelError("QuickInput.DisplayName", "L'identifiant est requis.");
        if (string.IsNullOrWhiteSpace(QuickInput.Pin) || QuickInput.Pin.Length != 4 || !QuickInput.Pin.All(char.IsDigit))
            ModelState.AddModelError("QuickInput.Pin", "Le code PIN doit contenir exactement 4 chiffres.");

        if (!ModelState.IsValid)
            return Page();

        var displayName = QuickInput.DisplayName!.Trim();
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DisplayName.ToUpper() == displayName.ToUpper());

        if (user == null
            || string.IsNullOrEmpty(user.PinHash)
            || await _userManager.IsInRoleAsync(user, AppRoles.Administrateur))
        {
            ModelState.AddModelError(string.Empty, "Identifiant ou code PIN incorrect");
            return Page();
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return RedirectToPage("./Lockout");
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PinHash, QuickInput.Pin!);
        if (verify == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Identifiant ou code PIN incorrect");
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString("O"));
        _logger.LogInformation("User logged in (PIN) as {DisplayName}.", user.DisplayName);
        return LocalRedirect(returnUrl);
    }
}
