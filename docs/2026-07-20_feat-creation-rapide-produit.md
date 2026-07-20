# Feat — Création rapide de produit (commande / réception)

**Date :** 2026-07-20  
**Commit :** `feat: add quick product creation modal in GoodsReceipts`

## Constat UX

`GoodsReceipts/Create` n’a **pas** de TomSelect produit : les lignes viennent d’une commande fournisseur déjà créée.  
Le sélecteur produit (TomSelect) est sur **`PurchaseOrders/Create`**.

## Implémentation

### API
- `POST /Products/CreateQuick` (`[FromForm] QuickProductDto` + antiforgery)
- Rôles : `AppRoles.Catalog` (Administrateur, Pharmacien, GestionnaireStock)
- Catégorie / fournisseur idempotents : « À catégoriser » / « Fournisseur non précisé »
- `ProductType = Inconnu`, stock 0, JSON `{ id, text, value, salePrice, purchasePrice }`

### UI
- Partial partagé : `Views/Shared/_QuickProductModal.cshtml`
- **PurchaseOrders/Create** : bouton « + » à côté de chaque TomSelect → crée + sélectionne
- **GoodsReceipts/Create** : bouton « Nouveau produit » (création sans quitter la page ; à rattacher ensuite à une commande)

## Test manuel

1. `PurchaseOrders/Create` → clic « + » → modale
2. Nom `TEST QUICK BL DOLIPRANE 500`, achat 500, vente 1200
3. Produit Id=11 créé, sélectionné dans TomSelect (`tomValue: "11"`)
4. Modale fermée, pas d’erreur

## Build / tests

- Build Release : 0 erreur, 0 warning
- Tests : 23/23 verts
