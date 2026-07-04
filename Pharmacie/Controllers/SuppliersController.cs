using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Catalog)]
public class SuppliersController : Controller
{
    private readonly ApplicationDbContext _context;

    public SuppliersController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Suppliers.OrderBy(s => s.Name).ToListAsync());
    }

    public async Task<IActionResult> IndexCsv()
    {
        var list = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N°"),
            ReportCsvFormatter.Escape("Nom"),
            ReportCsvFormatter.Escape("Contact"),
            ReportCsvFormatter.Escape("Téléphone")));

        foreach (var s in list)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.IntInvariant(s.Id),
                ReportCsvFormatter.Escape(s.Name),
                ReportCsvFormatter.Escape(s.Contact ?? ""),
                ReportCsvFormatter.Escape(s.Phone ?? "")));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("export-fournisseurs"));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(m => m.Id == id);
        if (supplier == null)
            return NotFound();

        return View(supplier);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Contact,Phone")] Supplier supplier)
    {
        if (ModelState.IsValid)
        {
            _context.Add(supplier);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(supplier);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
            return NotFound();

        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Contact,Phone")] Supplier supplier)
    {
        if (id != supplier.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(supplier);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await SupplierExistsAsync(supplier.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(supplier);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(m => m.Id == id);
        if (supplier == null)
            return NotFound();

        return View(supplier);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
            return RedirectToAction(nameof(Index));

        _context.Suppliers.Remove(supplier);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            TempData["Error"] =
                "Impossible de supprimer ce fournisseur car des produits y sont associés. Réaffectez ou supprimez ces produits d'abord.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> SupplierExistsAsync(int id) =>
        await _context.Suppliers.AnyAsync(e => e.Id == id);
}
