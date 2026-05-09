using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PatientTreatmentReminder
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Display(Name = "Type de rappel")]
    public PatientReminderType ReminderType { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date de rappel")]
    public DateTime ReminderDate { get; set; }

    [Required(ErrorMessage = "Le message est obligatoire.")]
    [StringLength(1000)]
    [Display(Name = "Message")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Fait")]
    public bool IsDone { get; set; }
}
