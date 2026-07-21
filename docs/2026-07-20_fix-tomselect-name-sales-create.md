# Fix — name/id explicites TomSelect sur Sales/Create

**Date :** 2026-07-20  
**Commit :** `fix: add explicit name attributes to TomSelect fields in Sales/Create`

## Avant

```html
<select asp-for="Lines[i].ProductId"
        class="form-select form-select-sm tomselect-product"
        data-tomselect-product
        data-val="false"
        data-url="...">
</select>
<input asp-for="Lines[i].Quantity" ... />
```

Sans `<option>` initiale ; warning DevTools sur l’input de recherche TomSelect (sans name/id).

## Après

```html
<select name="Lines[@i].ProductId" id="product-select-@i" ...>
  <option value="0"></option>
</select>
<input name="Lines[@i].Quantity" id="quantity-@i" value="..." />
```

+ `onInitialize` : `control_input.id = el.id + '-ts-control'`  
+ `allowEmptyOption: true`

## Test local

- Payload POST : `Lines[0].ProductId=8`, `Lines[0].Quantity=1`
- Vente #10 enregistrée ; stock produit 8 : 19 → 18
- Build 0 erreur — **23/23** tests
