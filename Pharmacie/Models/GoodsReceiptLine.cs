using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class GoodsReceiptLine
{
    public int Id { get; set; }

    public int GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }

    public int PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }

    [Display(Name = "Qté reçue")]
    public int QuantityReceived { get; set; }

    [StringLength(80)]
    [Display(Name = "N° lot")]
    public string LotNumber { get; set; } = string.Empty;

    [Display(Name = "Expiration")]
    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; }
}
