using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(120)]
    [Display(Name = "Nom")]
    public string Name { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
