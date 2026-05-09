using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.PatientsRead)]
public class PatientRemindersController : Controller
{
    private readonly ApplicationDbContext _db;

    public PatientRemindersController(ApplicationDbContext db)
    {
        _db = db;
    }

    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Create(int? patientId)
    {
        if (patientId == null || patientId <= 0)
            return BadRequest();

        if (!await _db.Patients.AnyAsync(p => p.Id == patientId))
            return NotFound();

        var vm = new PatientTreatmentReminder
        {
            PatientId = patientId.Value,
            ReminderDate = DateTime.Today,
            ReminderType = PatientReminderType.Treatment
        };
        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == patientId).Select(p => p.FullName).FirstAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Create(
        [Bind("PatientId,ReminderType,ReminderDate,Message,IsDone")] PatientTreatmentReminder reminder)
    {
        if (!await _db.Patients.AnyAsync(p => p.Id == reminder.PatientId))
            return NotFound();

        if (ModelState.IsValid)
        {
            _db.PatientTreatmentReminders.Add(reminder);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = reminder.PatientId });
        }

        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == reminder.PatientId).Select(p => p.FullName).FirstAsync();
        return View(reminder);
    }

    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var reminder = await _db.PatientTreatmentReminders.FindAsync(id);
        if (reminder == null)
            return NotFound();

        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == reminder.PatientId).Select(p => p.FullName).FirstAsync();
        return View(reminder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,PatientId,ReminderType,ReminderDate,Message,IsDone")] PatientTreatmentReminder reminder)
    {
        if (id != reminder.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _db.Update(reminder);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ReminderExistsAsync(reminder.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = reminder.PatientId });
        }

        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == reminder.PatientId).Select(p => p.FullName).FirstAsync();
        return View(reminder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var reminder = await _db.PatientTreatmentReminders.FindAsync(id);
        if (reminder == null)
            return NotFound();

        var patientId = reminder.PatientId;
        _db.PatientTreatmentReminders.Remove(reminder);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = patientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> MarkDone(int id)
    {
        var reminder = await _db.PatientTreatmentReminders.FindAsync(id);
        if (reminder == null)
            return NotFound();

        reminder.IsDone = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = reminder.PatientId });
    }

    private Task<bool> ReminderExistsAsync(int id) =>
        _db.PatientTreatmentReminders.AnyAsync(e => e.Id == id);
}
