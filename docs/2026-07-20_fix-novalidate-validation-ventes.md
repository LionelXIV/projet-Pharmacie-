# Fix — Déblocage validation des ventes (novalidate + jQuery Required)

**Date :** 2026-07-20  
**Commit message prévue :** `fix: add novalidate to Sales/Create to unblock sale validation`

## Corrections demandées (appliquées)

### CORRECTION 1 — `Views/Sales/Create.cshtml`
- `novalidate` ajouté sur `<form asp-action="Create" novalidate>`
- `data-val="false"` sur les selects produit (complément)

### CORRECTION 2 — `Views/GoodsReceipts/Create.cshtml`
- Même pattern `datetime-local` + pas de `novalidate`
- **Corrigé :** `<form asp-action="Create" method="post" novalidate>`

### CORRECTION 3 — `_Layout.cshtml`
- Sélecteur Flatpickr : exclusion `data-flatpickr-ignore` (en plus de `data-flatpickr-disabled`)

## Cause réelle découverte au test manuel (en plus de Flatpickr)

jQuery Unobtrusive Validation traitait chaque `ProductId` (`int` non-nullable) comme **Required** sur les 7 lignes vides → `valid: false`, submit bloqué **sans message visible**.

**Fix complémentaire :** `[ValidateNever]` sur `SaleLineSlotViewModel.ProductId` dans `SaleCreateViewModel.cs`  
(le contrôleur / SaleService / tests inchangés côté logique métier)

## Vérifications

| Check | Résultat |
|-------|----------|
| `dotnet build` Release | 0 erreur, 0 warning |
| `dotnet test` | **23/23 verts** |
| `novalidate` rendu HTML | **Oui** |
| jQuery `.valid()` après fix | **true** |
| POST vente (Crème mains, qté 1) | **OK** — vente Id=8, Notes=`test-manuel-novalidate`, redirect opaque |

## Fichiers touchés

- `Pharmacie/Views/Sales/Create.cshtml`
- `Pharmacie/Views/GoodsReceipts/Create.cshtml`
- `Pharmacie/Views/Shared/_Layout.cshtml`
- `Pharmacie/Models/SaleCreateViewModel.cs`

## Non modifiés (comme demandé)

SalesController (logique métier), SaleService, InventoryService, entités EF, migrations, tests.
