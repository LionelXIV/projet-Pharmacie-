# Diagnostic v2 — bouton « Valider la vente » (choix A–E)

**Date :** 2026-07-20  
**Portée :** DIAGNOSTIC UNIQUEMENT — aucun fichier modifié, pas de build/tests.  
**Symptôme client :** clic sur « Valider la vente » → rien ne se passe, vente non enregistrée.

---

## ÉTAPE 1 — `Views/Sales/Create.cshtml`

### Balise `<form>`

```html
<form asp-action="Create">
```

| Attribut | Valeur réelle |
|----------|----------------|
| `action` | Généré par Tag Helper → POST vers `/Sales/Create` (route courante Create) |
| `method` | **POST** (défaut Tag Helper `asp-action`, pas écrit explicitement dans le .cshtml) |
| `novalidate` | **Absent** → validation HTML5 du navigateur **active** |
| Antiforgery | Token injecté automatiquement par le Form Tag Helper |

### Bouton submit

```html
<button type="submit" class="btn btn-primary">…Valider la vente</button>
```

| Attribut | Valeur |
|----------|--------|
| `type` | `submit` |
| `class` | `btn btn-primary` |
| `data-confirm` | **Absent** |
| `id` | Absent |

### Selects TomSelect

```html
<select asp-for="Lines[i].ProductId"
        class="form-select form-select-sm tomselect-product"
        data-tomselect-product
        data-url="@Url.Action("Search", "Products")">
</select>
```

| Question | Réponse |
|----------|---------|
| `name` explicite ? | **Oui via Tag Helper** → `name="Lines[i].ProductId"` (et `id` associé) |
| `required` ? | **Non** |
| `<option value="">` ? | **Non** dans le markup — corps du `<select>` **vide** ; valeur CLR `ProductId = 0` peut générer une option `0` côté rendu, pas une option vide `""` |

### `@section Scripts`

1. `Html.RenderPartialAsync("_ValidationScriptsPartial")` → jQuery Validate + Unobtrusive  
2. Script TomSelect : init sur `[data-tomselect-product]`, AJAX `Products/Search`, `onChange` cherche un champ `UnitPrice` **inexistant** dans ce formulaire (no-op)

### jQuery Unobtrusive active ?

**Oui** — scripts de validation chargés dans la section Scripts.

### SweetAlert2 intercepte le submit de ce bouton ?

**Non** — le handler global (`_Layout.cshtml`) ne cible que `[data-confirm]`. Ce bouton n’a pas cet attribut.

---

## ÉTAPE 2 — `SalesController.Create` POST

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(SaleCreateViewModel model)
```

| Point | Comportement |
|-------|----------------|
| Si `ModelState` invalide / vente impossible | `return View(model)` |
| Message visible ? | Oui pour erreurs **modèle** via `asp-validation-summary="ModelOnly"` (ex. « Ajoutez au moins une ligne… », erreur `RecordSaleAsync`) |
| Erreurs de propriété | Via `asp-validation-for` (ex. `SoldAt`, `Notes`) |
| Antiforgery | **Oui** `[ValidateAntiForgeryToken]` |

Si le POST n’atteint jamais le serveur, cette étape n’explique pas le « clic mort ».

---

## ÉTAPE 3 — ViewModel (`SaleCreateViewModel` / `SaleLineSlotViewModel`)

Nom réel : **`SaleCreateViewModel`** (pas `CreateSaleViewModel`).

| Champ | Validations |
|-------|-------------|
| `SoldAt` | Aucun `[Required]` (non-nullable `DateTime`) |
| `Notes` | `[StringLength(500)]` seulement |
| `ProductId` | **Aucun** `[Required]` |
| `Quantity` | `[Range(0, int.MaxValue)]` |

Aucune validation ViewModel ne justifie un **blocage silencieux côté navigateur** sur le ProductId (pas de `required` HTML généré pour les produits).

---

## ÉTAPE 4 — Verdict A / B / C / D / E

| Option | Verdict | Pourquoi |
|--------|---------|----------|
| **A** TomSelect + `required` vide | **Écarté** | Les selects **n’ont pas** `required`. Un ProductId non propagé ferait quand même un POST → message serveur visible. |
| **B** SweetAlert2 | **Écarté** | Pas de `data-confirm` sur le bouton ; Swal ne s’applique pas. |
| **C** ModelState sans message | **Peu probable** comme cause du « rien ne se passe » | Les erreurs modèles sont affichées (`ModelOnly`). Une erreur serveur implique un **rechargement** de page. |
| **D** Erreur JS TomSelect | **Peu probable** | `if (!window.TomSelect) return;` — n’empêche pas le submit natif. |
| **E** Autre cause | **RETENUE** | Voir ci-dessous. |

### Cause exacte retenue : **E**

**Flatpickr global sur `input[type=datetime-local]` avec `altInput: true`**, appliqué au champ `SoldAt` de la vente.

Fichier : `Pharmacie/Views/Shared/_Layout.cshtml` (approx. lignes 312–320) :

```javascript
flatpickr("input[type='datetime-local']:not([data-flatpickr-disabled])", {
  locale: "fr",
  dateFormat: "Y-m-dTH:i",
  altInput: true,
  altFormat: "d/m/Y à H:i",
  enableTime: true,
  time_24hr: true,
  allowInput: true
});
```

Champ concerné : `Create.cshtml` ligne 25 :

```html
<input asp-for="SoldAt" class="form-control" type="datetime-local" />
```

- Pas de `novalidate` sur le form → HTML5 actif  
- Pas de `data-flatpickr-disabled` sur `SoldAt`  
- `altInput: true` masque l’input natif `datetime-local`  
- Si la valeur native est invalide / désynchronisée, le navigateur **refuse le submit** sur un champ **caché** → **aucun feedback visible** = symptôme client

---

## Résumé

| Élément | Valeur |
|---------|--------|
| **Cause** | **E** — Flatpickr (`altInput`) + `datetime-local` `SoldAt` + validation HTML5 |
| **Lignes / fichiers cause** | `_Layout.cshtml` ~312–320 ; `Sales/Create.cshtml` L19–25, L68 |
| **Fichiers à modifier pour corriger** | 1) `Pharmacie/Views/Shared/_Layout.cshtml` et/ou 2) `Pharmacie/Views/Sales/Create.cshtml` (ex. `data-flatpickr-disabled`, `novalidate`, ou format/sync Flatpickr) ; vérifier aussi `GoodsReceipts/Create.cshtml` (même `datetime-local`) |

**Non concernés en première intention :** SweetAlert2, `[Required]` ProductId, TomSelect `required`.
