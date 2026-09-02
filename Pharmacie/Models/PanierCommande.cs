using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class PanierCommande
{
    public int Id { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [Required]
    [StringLength(20)]
    public string Statut { get; set; } = "EnCours";

    public ICollection<PanierCommandeLigne> Lignes { get; set; } = new List<PanierCommandeLigne>();
}
