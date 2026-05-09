using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PatientPrescription
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date ordonnance")]
    public DateTime PrescribedAt { get; set; }

    [Required(ErrorMessage = "Le nom du médecin est obligatoire.")]
    [StringLength(200)]
    [Display(Name = "Médecin")]
    public string DoctorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le contenu de l’ordonnance est obligatoire.")]
    [Display(Name = "Contenu")]
    public string Content { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date de renouvellement")]
    public DateTime? RenewalDate { get; set; }

    [Display(Name = "Statut")]
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Active;
}
