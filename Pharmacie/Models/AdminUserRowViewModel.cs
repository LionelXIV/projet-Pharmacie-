namespace Pharmacie.Models;

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RolesDisplay { get; set; } = string.Empty;
    public List<string> RoleLabels { get; set; } = new();
    public string LoginType { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
    public bool IsTitulaire { get; set; }
}
