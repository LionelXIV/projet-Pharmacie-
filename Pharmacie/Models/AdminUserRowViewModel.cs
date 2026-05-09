namespace Pharmacie.Models;

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RolesDisplay { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
}
