using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacie.Authorization;

namespace Pharmacie.Controllers;

/// <summary>Centre de téléchargements — réservé aux rôles Finances (Titulaire / Comptable).</summary>
[Authorize(Roles = AppRoles.FinancesAccess)]
public class DocumentsController : Controller
{
    [HttpGet]
    public IActionResult Index(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var today = DateTime.Today;
        ViewBag.DateDebut = (dateDebut ?? today.AddDays(-29)).ToString("yyyy-MM-dd");
        ViewBag.DateFin = (dateFin ?? today).ToString("yyyy-MM-dd");
        ViewBag.DateJour = today.ToString("yyyy-MM-dd");
        return View();
    }
}
