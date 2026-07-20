# Feat — Filtres de recherche sur GoodsReceipts/Index

**Date :** 2026-07-20  
**Commit :** `feat: add advanced search filters to GoodsReceipts/Index`

## Audit préalable

- **Pas d’action `Index`** ni de vue `Index.cshtml` avant cette feature (seulement `Create`).
- Entité `GoodsReceipt` : `Id`, `PurchaseOrderId`, `ReceivedAt`, `Notes` — **pas de champ « numéro BL » dédié**.
- Fournisseur accessible via `PurchaseOrder.Supplier`.
- Réceptions visibles uniquement dans `PurchaseOrders/Details`.

## Implémentation

- Création de `Index` (liste + pagination 50) avec filtres GET :
  - `searchNumber` → `Id.ToString()` / `Notes` (Contains)
  - `searchSupplier` → `Supplier.Name` (Contains, insensible à la casse)
  - `dateFrom` / `dateTo` → `ReceivedAt` (jours calendaires inclusifs)
- Formulaire de filtres + badge « Filtres actifs » ; dates avec `data-flatpickr-ignore="true"`.
- Lien menu **Bons de livraison** ; détail via commande associée.

## Vérifications

- Liste sans filtre OK ; filtres numéro / fournisseur / dates OK ; effacement OK.
- `dotnet build` 0 erreur / 0 warning ; `dotnet test` **23/23**.
