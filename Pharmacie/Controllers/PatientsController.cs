using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.PatientsRead)]
public class PatientsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PatientsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] string? active)
    {
        var query = FilteredPatientsQuery(q, active);
        var list = await query.OrderBy(p => p.FullName).ToListAsync();
        ViewBag.Query = q;
        ViewBag.Active = active;
        ViewBag.CanManage = AppRoles.CanManagePatients(User);
        return View(list);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var patient = await _db.Patients
            .Include(p => p.Prescriptions)
            .Include(p => p.TreatmentReminders)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null)
            return NotFound();

        ViewBag.CanManage = AppRoles.CanManagePatients(User);
        return View(patient);
    }

    public async Task<IActionResult> IndexCsv([FromQuery] string? q, [FromQuery] string? active)
    {
        var list = await FilteredPatientsQuery(q, active).OrderBy(p => p.FullName).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N° fiche"),
            ReportCsvFormatter.Escape("Nom complet"),
            ReportCsvFormatter.Escape("Téléphone"),
            ReportCsvFormatter.Escape("Courriel"),
            ReportCsvFormatter.Escape("Adresse"),
            ReportCsvFormatter.Escape("Naissance"),
            ReportCsvFormatter.Escape("Actif"),
            ReportCsvFormatter.Escape("Notes"),
            ReportCsvFormatter.Escape("Allergies"),
            ReportCsvFormatter.Escape("Pathologie chronique"),
            ReportCsvFormatter.Escape("Traitement habituel"),
            ReportCsvFormatter.Escape("Médecin traitant")));

        foreach (var p in list)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.IntInvariant(p.Id),
                ReportCsvFormatter.Escape(p.FullName),
                ReportCsvFormatter.Escape(p.Phone),
                ReportCsvFormatter.Escape(p.Email ?? ""),
                ReportCsvFormatter.Escape(p.Address ?? ""),
                ReportCsvFormatter.Escape(p.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
                p.IsActive ? ReportCsvFormatter.Escape("Oui") : ReportCsvFormatter.Escape("Non"),
                ReportCsvFormatter.Escape(p.Notes ?? ""),
                ReportCsvFormatter.Escape(p.Allergies ?? ""),
                ReportCsvFormatter.Escape(p.ChronicCondition ?? ""),
                ReportCsvFormatter.Escape(p.UsualTreatment ?? ""),
                ReportCsvFormatter.Escape(p.TreatingDoctor ?? "")));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("export-patients"));
    }

    public async Task<IActionResult> DetailsCsv(int? id)
    {
        if (id == null)
            return NotFound();

        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null)
            return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N° fiche"),
            ReportCsvFormatter.Escape("Nom complet"),
            ReportCsvFormatter.Escape("Téléphone"),
            ReportCsvFormatter.Escape("Courriel"),
            ReportCsvFormatter.Escape("Adresse"),
            ReportCsvFormatter.Escape("Naissance"),
            ReportCsvFormatter.Escape("Actif"),
            ReportCsvFormatter.Escape("Notes"),
            ReportCsvFormatter.Escape("Allergies"),
            ReportCsvFormatter.Escape("Pathologie chronique"),
            ReportCsvFormatter.Escape("Traitement habituel"),
            ReportCsvFormatter.Escape("Médecin traitant")));

        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.IntInvariant(p.Id),
            ReportCsvFormatter.Escape(p.FullName),
            ReportCsvFormatter.Escape(p.Phone),
            ReportCsvFormatter.Escape(p.Email ?? ""),
            ReportCsvFormatter.Escape(p.Address ?? ""),
            ReportCsvFormatter.Escape(p.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
            p.IsActive ? ReportCsvFormatter.Escape("Oui") : ReportCsvFormatter.Escape("Non"),
            ReportCsvFormatter.Escape(p.Notes ?? ""),
            ReportCsvFormatter.Escape(p.Allergies ?? ""),
            ReportCsvFormatter.Escape(p.ChronicCondition ?? ""),
            ReportCsvFormatter.Escape(p.UsualTreatment ?? ""),
            ReportCsvFormatter.Escape(p.TreatingDoctor ?? "")));

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName($"fiche-patient-{p.Id}"));
    }

    public async Task<IActionResult> PrescriptionsCsv([FromQuery] int? patientId)
    {
        var q = _db.PatientPrescriptions.AsNoTracking().Include(r => r.Patient).AsQueryable();
        if (patientId.HasValue)
            q = q.Where(r => r.PatientId == patientId.Value);

        var rows = await q.OrderByDescending(r => r.PrescribedAt).ThenByDescending(r => r.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N° ordonnance"),
            ReportCsvFormatter.Escape("N° patient"),
            ReportCsvFormatter.Escape("Patient"),
            ReportCsvFormatter.Escape("Date ordonnance"),
            ReportCsvFormatter.Escape("Médecin"),
            ReportCsvFormatter.Escape("Contenu"),
            ReportCsvFormatter.Escape("Date renouvellement"),
            ReportCsvFormatter.Escape("Statut")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.IntInvariant(r.Id),
                ReportCsvFormatter.IntInvariant(r.PatientId),
                ReportCsvFormatter.Escape(r.Patient?.FullName ?? ""),
                ReportCsvFormatter.Escape(r.PrescribedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.Escape(r.DoctorName),
                ReportCsvFormatter.Escape(r.Content),
                ReportCsvFormatter.Escape(r.RenewalDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
                ReportCsvFormatter.Escape(PrescriptionStatusLabel(r.Status))));
        }

        var slug = patientId.HasValue ? $"ordonnances-patient-{patientId.Value}" : "export-ordonnances";
        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName(slug));
    }

    public async Task<IActionResult> RemindersCsv([FromQuery] int? patientId)
    {
        var q = _db.PatientTreatmentReminders.AsNoTracking().Include(r => r.Patient).AsQueryable();
        if (patientId.HasValue)
            q = q.Where(r => r.PatientId == patientId.Value);

        var rows = await q.OrderBy(r => r.ReminderDate).ThenBy(r => r.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N° rappel"),
            ReportCsvFormatter.Escape("N° patient"),
            ReportCsvFormatter.Escape("Patient"),
            ReportCsvFormatter.Escape("Type"),
            ReportCsvFormatter.Escape("Date rappel"),
            ReportCsvFormatter.Escape("Message"),
            ReportCsvFormatter.Escape("Fait")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.IntInvariant(r.Id),
                ReportCsvFormatter.IntInvariant(r.PatientId),
                ReportCsvFormatter.Escape(r.Patient?.FullName ?? ""),
                ReportCsvFormatter.Escape(ReminderTypeLabel(r.ReminderType)),
                ReportCsvFormatter.Escape(r.ReminderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.Escape(r.Message),
                r.IsDone ? ReportCsvFormatter.Escape("Oui") : ReportCsvFormatter.Escape("Non")));
        }

        var slug = patientId.HasValue ? $"rappels-patient-{patientId.Value}" : "export-rappels-patients";
        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName(slug));
    }

    [Authorize(Roles = AppRoles.PatientsManage)]
    public IActionResult Create()
    {
        return View(new Patient());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Create(
        [Bind("FullName,Phone,Email,Address,DateOfBirth,Notes,IsActive,Allergies,ChronicCondition,UsualTreatment,TreatingDoctor")]
        Patient patient)
    {
        if (ModelState.IsValid)
        {
            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = patient.Id });
        }

        return View(patient);
    }

    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var patient = await _db.Patients.FindAsync(id);
        if (patient == null)
            return NotFound();

        return View(patient);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,FullName,Phone,Email,Address,DateOfBirth,Notes,IsActive,Allergies,ChronicCondition,UsualTreatment,TreatingDoctor")]
        Patient patient)
    {
        if (id != patient.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _db.Update(patient);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await PatientExistsAsync(patient.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Details), new { id = patient.Id });
        }

        return View(patient);
    }

    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null)
            return NotFound();

        return View(patient);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.PatientsManage)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient != null)
        {
            _db.Patients.Remove(patient);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<bool> PatientExistsAsync(int id) =>
        _db.Patients.AnyAsync(e => e.Id == id);

    private IQueryable<Patient> FilteredPatientsQuery(string? q, string? active)
    {
        var query = _db.Patients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p =>
                p.FullName.Contains(term)
                || p.Phone.Contains(term)
                || (p.Email != null && p.Email.Contains(term)));
        }

        if (active == "0")
            query = query.Where(p => !p.IsActive);
        else if (active == "1")
            query = query.Where(p => p.IsActive);

        return query;
    }

    private static string PrescriptionStatusLabel(PrescriptionStatus s) => s switch
    {
        PrescriptionStatus.Active => "Active",
        PrescriptionStatus.Archived => "Archivée",
        _ => s.ToString()
    };

    private static string ReminderTypeLabel(PatientReminderType t) => t switch
    {
        PatientReminderType.Treatment => "Traitement",
        PatientReminderType.PrescriptionRenewal => "Renouvellement ordonnance",
        PatientReminderType.ClientCall => "Appel client",
        _ => t.ToString()
    };
}
