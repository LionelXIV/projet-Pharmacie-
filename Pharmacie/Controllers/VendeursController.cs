using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles = $"{AppRoles.CanManageUsers},{AppRoles.Administrateur}")]
public class VendeursController : Controller
{
    private static readonly string[] CouleursTicket =
    [
        "Bleu",
        "Jaune",
        "Vert",
        "Rose",
        "Autre"
    ];

    private readonly ApplicationDbContext _context;

    public VendeursController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _context.Vendeurs
            .AsNoTracking()
            .OrderByDescending(v => v.IsActif)
            .ThenBy(v => v.Nom)
            .ToListAsync();
        return View(list);
    }

    public IActionResult Create()
    {
        PopulateCouleurs();
        return View(new Vendeur());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vendeur model)
    {
        if (string.IsNullOrWhiteSpace(model.Nom))
            ModelState.AddModelError(nameof(model.Nom), "Le nom du vendeur est obligatoire.");

        if (ModelState.IsValid)
        {
            model.Nom = model.Nom.Trim();
            model.CouleurTicket = string.IsNullOrWhiteSpace(model.CouleurTicket) ? null : model.CouleurTicket.Trim();
            model.IsActif = true;
            model.CreatedAt = DateTime.UtcNow;
            _context.Vendeurs.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Vendeur « {model.Nom} » créé.";
            return RedirectToAction(nameof(Index));
        }

        PopulateCouleurs(model.CouleurTicket);
        return View(model);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var vendeur = await _context.Vendeurs.FindAsync(id);
        if (vendeur == null)
            return NotFound();

        PopulateCouleurs(vendeur.CouleurTicket);
        return View(vendeur);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vendeur model)
    {
        if (id != model.Id)
            return NotFound();

        if (string.IsNullOrWhiteSpace(model.Nom))
            ModelState.AddModelError(nameof(model.Nom), "Le nom du vendeur est obligatoire.");

        if (ModelState.IsValid)
        {
            var vendeur = await _context.Vendeurs.FindAsync(id);
            if (vendeur == null)
                return NotFound();

            vendeur.Nom = model.Nom.Trim();
            vendeur.CouleurTicket = string.IsNullOrWhiteSpace(model.CouleurTicket) ? null : model.CouleurTicket.Trim();
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Vendeur « {vendeur.Nom} » mis à jour.";
            return RedirectToAction(nameof(Index));
        }

        PopulateCouleurs(model.CouleurTicket);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var vendeur = await _context.Vendeurs.FindAsync(id);
        if (vendeur == null)
            return NotFound();

        vendeur.IsActif = !vendeur.IsActif;
        await _context.SaveChangesAsync();
        TempData["Success"] = vendeur.IsActif
            ? $"Vendeur « {vendeur.Nom} » activé."
            : $"Vendeur « {vendeur.Nom} » désactivé.";
        return RedirectToAction(nameof(Index));
    }

    private void PopulateCouleurs(string? selected = null)
    {
        ViewBag.Couleurs = new SelectList(CouleursTicket, selected);
    }
}
