using Microsoft.AspNetCore.Mvc.Rendering;

namespace Pharmacie.Models;

public class ProductClassificationIndexViewModel
{
    public List<ProductClassificationRowViewModel> Products { get; set; } = new();

    public string? Term { get; set; }

    public int? FilterType { get; set; }

    public int CurrentPage { get; set; }

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }

    public int UnknownCount { get; set; }

    public List<SelectListItem> ProductTypes { get; set; } = new();
}

public class ProductClassificationRowViewModel
{
    public int Id { get; set; }

    public string? Cip { get; set; }

    public string CommercialName { get; set; } = string.Empty;

    public ProductType ProductType { get; set; }

    public string? SupplierName { get; set; }
}
