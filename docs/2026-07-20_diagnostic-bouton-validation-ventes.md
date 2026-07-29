# Diagnostic — bouton « Valider la vente » (lecture seule)

**Date :** 2026-07-20  
**Contexte :** Client signale que le bouton de validation des ventes ne fonctionne pas — aucune action au clic.  
**Portée :** DIAGNOSTIC UNIQUEMENT — aucun fichier modifié.

---

## ÉTAPE 1 — Formulaire `Views/Sales/Create.cshtml`

| Élément | Détail |
|--------|--------|
| **Formulaire** | `<form asp-action="Create">` → Tag Helper = **POST** vers `Sales/Create`, token antiforgery injecté automatiquement |
| **Bouton** | `<button type="submit" class="btn btn-primary">` — **pas d’`id`**, **pas de `data-*`**, texte « Valider la vente » |
| **Scripts** | `_ValidationScriptsPartial` (jQuery Validate + Unobtrusive) + init **TomSelect** sur `[data-tomselect-product]` |
| **SweetAlert2 sur ce bouton ?** | **Non** — aucun `data-confirm`, aucun `Swal` dans cette vue |
| **Validation Unobtrusive ?** | **Oui** (partial rendu dans `@section Scripts`) |
| **TomSelect ?** | **Oui** — chaque `select` produit a `class="tomselect-product"` + `data-tomselect-product` + `data-url` → `Products/Search` |

---

## ÉTAPE 2 — Contrôleur `SalesController.Create` POST

Fichier : `Pharmacie/Controllers/SalesController.cs`

| Point | Valeur |
|------|--------|
| **Signature** | `[HttpPost] Create(SaleCreateViewModel model)` |
| **Antiforgery** | **Oui** `[ValidateAntiForgeryToken]` |
| **Succès** | `RedirectToAction(Details, id)` |
| **Erreur** | `ModelState` + `return View(model)` — message via `asp-validation-summary="ModelOnly"` |

Logique : filtre les lignes `ProductId > 0 && Quantity > 0` ; si aucune ligne → erreur modèle ; sinon `SaleService.RecordSaleAsync`.

---

## ÉTAPE 3 — ViewModel `SaleCreateViewModel`

Fichier : `Pharmacie/Models/SaleCreateViewModel.cs`

- `SoldAt` : `DateTime` (défaut `DateTime.Now`), `[DataType(DateTime)]`
- `Notes` : `string?`, `[StringLength(500)]`
- `Lines` : `List<SaleLineSlotViewModel>`
  - `ProductId` : `int` (défaut 0) — **pas de `[Required]`**
  - `Quantity` : `int`, `[Range(0, int.MaxValue)]`

---

## ÉTAPE 4 — Causes probables (par priorité)

### 1. Cause la plus probable — Flatpickr + `datetime-local` (blocage HTML5 silencieux)

Dans `Pharmacie/Views/Shared/_Layout.cshtml` (global) :

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

Le champ `SoldAt` est un `datetime-local` **sans** `data-flatpickr-disabled`. Avec `altInput: true`, Flatpickr masque l’input natif et affiche un champ alternatif. Si la valeur native est invalide / vide / mal synchronisée, le navigateur **bloque le submit** sur le champ caché → **aucune navigation, aucun message visible**. Cela colle au symptôme « le clic ne fait rien ».

### 2. TomSelect — valeur non propagée (symptôme différent)

Si l’UI montre un produit mais le `<select>` sous-jacent reste à `0`, le POST part quand même ; le serveur répond avec *« Ajoutez au moins une ligne… »*. Ce serait une **recharge de page avec message**, pas un clic mort — sauf si le message n’est pas remarqué.

### 3. SweetAlert2 — écarté pour ce bouton

Le handler global n’écoute que `[data-confirm]`. Le bouton vente **n’a pas** cet attribut → Swal **n’intercepte pas** ce clic.

### 4. Erreur JS silencieuse — peu probable comme cause unique

L’init TomSelect fait `if (!window.TomSelect) return;` → échec TomSelect ≠ blocage submit. Le `onChange` cherche un `UnitPrice` **absent du formulaire** → no-op.

### 5. ModelState serveur sans feedback — possible en second rideau

Si le POST aboutit mais `SoldAt` ne bind pas, ou 0 ligne valide → `return View(model)`. Moins probable si vraiment **aucun** rechargement.

### 6. Validation jQuery Unobtrusive — faible

Peu de contraintes côté client. Peu susceptible de bloquer un formulaire correctement rempli.

---

## Résumé

| Question | Réponse |
|----------|---------|
| **Structure form / bouton** | POST `Create`, submit simple `btn btn-primary`, pas de `data-confirm` |
| **Cause n°1** | **Flatpickr global sur `datetime-local` + `altInput`** → validation HTML5 sur input masqué → clic sans effet |
| **Cause n°2** | TomSelect / lignes vides → POST OK mais erreur serveur |
| **Écarté** | SweetAlert sur ce bouton |

### Fichiers à toucher pour la correction (étape suivante)

1. `Pharmacie/Views/Shared/_Layout.cshtml` — Flatpickr / `datetime-local`
2. `Pharmacie/Views/Sales/Create.cshtml` — `SoldAt` (`data-flatpickr-disabled`, format, etc.)
3. Éventuellement `Pharmacie/Views/GoodsReceipts/Create.cshtml` — même pattern
4. Secondaire : TomSelect / messages d’erreur Create + contrôleur
