using System.ComponentModel.DataAnnotations;

namespace Pharmacie.Models;

public class ProductImportUploadViewModel
{
    [Display(Name = "Fichier Excel (.xlsx)")]
    public IFormFile? File { get; set; }
}
