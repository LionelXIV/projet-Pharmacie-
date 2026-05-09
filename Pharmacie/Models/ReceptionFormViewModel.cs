using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class ReceptionFormViewModel
{
    public int PurchaseOrderId { get; set; }

    [Display(Name = "Date de réception")]
    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<ReceptionLineRowViewModel> Lines { get; set; } = new();
}

public class ReceptionLineRowViewModel
{
    public int PurchaseOrderLineId { get; set; }

    [Display(Name = "Produit")]
    public string ProductName { get; set; } = string.Empty;

    public int QuantityOrdered { get; set; }
    public int QuantityReceivedBefore { get; set; }

    public int Remaining => Math.Max(0, QuantityOrdered - QuantityReceivedBefore);

    [Display(Name = "Qté à réceptionner")]
    [Range(0, int.MaxValue)]
    public int QuantityReceived { get; set; }

    [StringLength(80)]
    [Display(Name = "N° lot")]
    public string? LotNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Expiration")]
    public DateTime? ExpirationDate { get; set; }
}
