using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Sales)]
public class SalesController : Controller
{
    private const int IndexPageSize = 50;

    private readonly ApplicationDbContext _context;
    private readonly SaleService _sales;

    public SalesController(ApplicationDbContext context, SaleService sales)
    {
        _context = context;
        _sales = sales;
    }

    public async Task<IActionResult> Index([FromQuery] SaleListFilters? filter, int page = 1)
    {
        filter ??= new SaleListFilters();
        if (page < 1)
            page = 1;

        var q = _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .AsQueryable();

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.Date;
            q = q.Where(s => s.SoldAt >= from);
        }

        if (filter.To.HasValue)
        {
            var toExclusive = filter.To.Value.Date.AddDays(1);
            q = q.Where(s => s.SoldAt < toExclusive);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
            q = q.Where(s => s.UserId == filter.UserId);

        var totalCount = await q.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)IndexPageSize);
        if (page > totalPages)
            page = totalPages;

        var list = await q
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * IndexPageSize)
            .Take(IndexPageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.UserLabels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, list.Select(s => s.UserId));
        await PopulateSaleFilterUsersAsync(filter.UserId);
        return View(new SaleIndexPageViewModel { Filter = filter, Sales = list });
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null)
            return NotFound();

        ViewBag.RecordedBy = string.IsNullOrEmpty(sale.UserId)
            ? "—"
            : UserDisplayResolver.Resolve(
                await UserDisplayResolver.LoadLabelsByIdAsync(_context, new[] { sale.UserId }),
                sale.UserId);

        return View(sale);
    }

    public async Task<IActionResult> DetailsCsv(int? id)
    {
        if (id == null)
            return NotFound();

        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null)
            return NotFound();

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(
            _context,
            string.IsNullOrEmpty(sale.UserId) ? Array.Empty<string>() : new[] { sale.UserId });
        var recordedBy = string.IsNullOrEmpty(sale.UserId)
            ? ""
            : UserDisplayResolver.Resolve(labels, sale.UserId);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Date vente"),
            ReportCsvFormatter.Escape("Enregistré par"),
            ReportCsvFormatter.Escape("Moyen de paiement"),
            ReportCsvFormatter.Escape("Notes")));
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.IntInvariant(sale.Id),
            ReportCsvFormatter.Escape(sale.SoldAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            ReportCsvFormatter.Escape(recordedBy),
            ReportCsvFormatter.Escape(PaymentMethodDisplay.GetName(sale.PaymentMethod)),
            ReportCsvFormatter.Escape(sale.Notes ?? "")));

        sb.AppendLine();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("Ligne"),
            ReportCsvFormatter.Escape("N° produit"),
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Prix unit. (FCFA)"),
            ReportCsvFormatter.Escape("Qté"),
            ReportCsvFormatter.Escape("Sous-total (FCFA)")));

        var lineNo = 1;
        foreach (var l in sale.Lines.OrderBy(x => x.Id))
        {
            var sub = l.Quantity * l.UnitPrice;
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.IntInvariant(lineNo++),
                ReportCsvFormatter.IntInvariant(l.ProductId),
                ReportCsvFormatter.Escape(l.Product?.CommercialName ?? ""),
                ReportCsvFormatter.FcfaCsvAmount(l.UnitPrice),
                ReportCsvFormatter.IntInvariant(l.Quantity),
                ReportCsvFormatter.FcfaCsvAmount(sub)));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName($"vente-{sale.Id}-lignes"));
    }

    public IActionResult Create()
    {
        var vm = new SaleCreateViewModel
        {
            Lines = Enumerable.Range(0, 8).Select(_ => new SaleLineSlotViewModel()).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaleCreateViewModel model)
    {
        var slots = model.Lines ?? new List<SaleLineSlotViewModel>();
        var lines = slots
            .Where(l => l.ProductId > 0 && l.Quantity > 0)
            .Select(l => (l.ProductId, l.Quantity))
            .ToList();

        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Ajoutez au moins une ligne avec un produit et une quantité.");

        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (ok, error, saleId) = await _sales.RecordSaleAsync(
                model.SoldAt,
                model.Notes,
                lines,
                userId,
                model.PaymentMethod);
            if (ok && saleId.HasValue)
                return RedirectToAction(nameof(Details), new { id = saleId.Value });

            ModelState.AddModelError(string.Empty, error ?? "Vente impossible.");
        }

        if (model.Lines == null || model.Lines.Count == 0)
        {
            model.Lines = Enumerable.Range(0, 8).Select(_ => new SaleLineSlotViewModel()).ToList();
        }

        return View(model);
    }

    private async Task PopulateSaleFilterUsersAsync(string? selectedUserId)
    {
        var userIds = await _context.Sales
            .AsNoTracking()
            .Where(s => s.UserId != null && s.UserId != "")
            .Select(s => s.UserId!)
            .Distinct()
            .ToListAsync();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        var userItems = users
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = UserDisplayResolver.Format(u.Email, u.UserName),
                Selected = u.Id == selectedUserId
            })
            .ToList();

        ViewData["FilterUserId"] = userItems;
    }
}
