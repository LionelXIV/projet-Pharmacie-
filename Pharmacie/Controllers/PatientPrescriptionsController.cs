using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.PatientsRead)]
public class PatientPrescriptionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PatientPrescriptionsController(ApplicationDbContext db)
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

        var vm = new PatientPrescription { PatientId = patientId.Value, PrescribedAt = DateTime.Today };
        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == patientId).Select(p => p.FullName).FirstAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Create(
        [Bind("PatientId,PrescribedAt,DoctorName,Content,RenewalDate,Status")] PatientPrescription prescription)
    {
        if (!await _db.Patients.AnyAsync(p => p.Id == prescription.PatientId))
            return NotFound();

        if (ModelState.IsValid)
        {
            _db.PatientPrescriptions.Add(prescription);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = prescription.PatientId });
        }

        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == prescription.PatientId).Select(p => p.FullName).FirstAsync();
        return View(prescription);
    }

    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var prescription = await _db.PatientPrescriptions.FindAsync(id);
        if (prescription == null)
            return NotFound();

        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == prescription.PatientId).Select(p => p.FullName).FirstAsync();
        return View(prescription);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,PatientId,PrescribedAt,DoctorName,Content,RenewalDate,Status")] PatientPrescription prescription)
    {
        if (id != prescription.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _db.Update(prescription);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await PrescriptionExistsAsync(prescription.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = prescription.PatientId });
        }

        ViewBag.PatientName = await _db.Patients.Where(p => p.Id == prescription.PatientId).Select(p => p.FullName).FirstAsync();
        return View(prescription);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var prescription = await _db.PatientPrescriptions.FindAsync(id);
        if (prescription == null)
            return NotFound();

        var patientId = prescription.PatientId;
        _db.PatientPrescriptions.Remove(prescription);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(PatientsController.Details), "Patients", new { id = patientId });
    }

    private Task<bool> PrescriptionExistsAsync(int id) =>
        _db.PatientPrescriptions.AnyAsync(e => e.Id == id);
}
