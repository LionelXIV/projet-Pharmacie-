using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(200)]
    [Display(Name = "Nom")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Contact")]
    public string? Contact { get; set; }

    [StringLength(40)]
    [Display(Name = "Téléphone")]
    public string? Phone { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
