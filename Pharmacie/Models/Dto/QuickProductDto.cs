namespace Pharmacie.Models.Dto;

public class QuickProductDto
{
    public string Name { get; set; } = "";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int CategoryId { get; set; }
    public string? Cip { get; set; }
    public int? SupplierId { get; set; }
}
