using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class Vendeur
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom du vendeur est obligatoire.")]
    [StringLength(100)]
    [Display(Name = "Nom")]
    public string Nom { get; set; } = "";

    /// <summary>Couleur de ticket optionnelle (repère visuel).</summary>
    [StringLength(50)]
    [Display(Name = "Couleur ticket")]
    public string? CouleurTicket { get; set; }

    [Display(Name = "Actif")]
    public bool IsActif { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
