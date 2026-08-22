using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacie.Authorization;

namespace Pharmacie.Controllers;

[Authorize(Roles = $"{AppRoles.GoodsReceipt},{AppRoles.Administrateur}")]
public class BLImportController : Controller
{
    [HttpGet]
    public IActionResult Upload()
        => RedirectToAction("CreateDirect", "GoodsReceipts", new { pdf = 1 });
}
