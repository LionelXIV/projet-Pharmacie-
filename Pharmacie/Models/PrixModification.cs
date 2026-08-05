using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pharmacie.Models;

public class PrixModification
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AncienPrix { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NouveauPrix { get; set; }

    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    [StringLength(450)]
    public string ModifiedByUserId { get; set; } = "";

    [StringLength(200)]
    public string ModifiedByDisplayName { get; set; } = "";

    [StringLength(200)]
    public string Raison { get; set; } = "";
}
