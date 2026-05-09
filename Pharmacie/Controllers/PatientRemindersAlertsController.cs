using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.PatientsRead)]
public class PatientRemindersAlertsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PatientRemindersAlertsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var list = await _db.PatientTreatmentReminders
            .AsNoTracking()
            .Include(r => r.Patient)
            .Where(r => !r.IsDone && r.ReminderDate <= today)
            .OrderBy(r => r.ReminderDate)
            .ThenBy(r => r.Patient!.FullName)
            .ToListAsync();

        ViewBag.CanManage = AppRoles.CanManagePatients(User);
        ViewBag.Today = today;
        return View(list);
    }
}
