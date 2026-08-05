namespace Pharmacie.Models;

public class BonCreateFromVenteViewModel
{
    public string ClientNom { get; set; } = "";
    public string? ClientTelephone { get; set; }
    public string? NumeroIdentite { get; set; }
    public int? VendeurId { get; set; }
    public List<BonLigneSlotViewModel> Lines { get; set; } = new();
}

public class BonLigneSlotViewModel
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal DiscountPercent { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public string DiscountType { get; set; } = "";
}
