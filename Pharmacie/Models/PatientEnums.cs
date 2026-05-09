namespace Pharmacie.Models;

public enum PrescriptionStatus
{
    Active = 0,
    Archived = 1
}

public enum PatientReminderType
{
    Treatment = 0,
    PrescriptionRenewal = 1,
    ClientCall = 2
}
