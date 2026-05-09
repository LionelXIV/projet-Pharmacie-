using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class Patient
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom complet est obligatoire.")]
    [StringLength(200)]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le téléphone est obligatoire.")]
    [StringLength(40)]
    [Display(Name = "Téléphone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(200)]
    [EmailAddress]
    [Display(Name = "Courriel")]
    public string? Email { get; set; }

    [StringLength(500)]
    [Display(Name = "Adresse")]
    public string? Address { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(2000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Actif")]
    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    [Display(Name = "Allergies")]
    public string? Allergies { get; set; }

    [StringLength(1000)]
    [Display(Name = "Pathologie chronique")]
    public string? ChronicCondition { get; set; }

    [StringLength(1000)]
    [Display(Name = "Traitement habituel")]
    public string? UsualTreatment { get; set; }

    [StringLength(200)]
    [Display(Name = "Médecin traitant")]
    public string? TreatingDoctor { get; set; }

    public ICollection<PatientPrescription> Prescriptions { get; set; } = new List<PatientPrescription>();
    public ICollection<PatientTreatmentReminder> TreatmentReminders { get; set; } = new List<PatientTreatmentReminder>();
}
