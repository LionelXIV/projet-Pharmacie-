namespace Pharmacie.Models;

public static class PatientUiLabels
{
    public static string ReminderType(PatientReminderType t) => t switch
    {
        PatientReminderType.Treatment => "Traitement",
        PatientReminderType.PrescriptionRenewal => "Renouvellement ordonnance",
        PatientReminderType.ClientCall => "Appel client",
        _ => t.ToString()
    };

    public static string PrescriptionStatusLabel(PrescriptionStatus s) => s switch
    {
        PrescriptionStatus.Active => "Actif",
        PrescriptionStatus.Archived => "Archivé",
        _ => s.ToString()
    };
}
