# Audit module vente — refonte interface (lecture seule)

**Date :** 2026-07-29  
**Projet :** Pharmacie — ASP.NET Core 9 MVC  
**Contexte :** 23/23 tests verts, build 0 erreur au moment de l’audit  
**Objectif :** comprendre l’état actuel du module vente avant refonte UI type POS  
**Périmètre :** aucune modification de code — diagnostic uniquement

---

## 1. Structure actuelle complète

### 1.1 `SalesController` — actions

| Action | Verbe | Rôle |
|--------|--------|------|
| `Index` | GET | Liste paginée (50/page), filtres `From` / `To` / `UserId`, includes `Lines`, `Vendeur` |
| `Details` | GET | Détail vente + lignes + produits |
| `DetailsCsv` | GET | Export CSV en-tête + lignes |
| `Create` | GET | Formulaire avec **8 emplacements** de lignes vides |
| `Create` | POST | Valide, appelle `SaleService.RecordSaleAsync`, puis assigne `VendeurId` en base |

**Autorisation :** `[Authorize(Roles = AppRoles.Sales)]` sur tout le contrôleur.

**Hors vue mais lié :** `ProductsController.Search` (JSON TomSelect) — politique `ProductSearch` (ventes, catalogue, achats).

**Fichiers lus :**

- `Pharmacie/Controllers/SalesController.cs`
- `Pharmacie/Services/SaleService.cs`
- `Pharmacie/Models/Sale.cs`
- `Pharmacie/Models/SaleLine.cs`
- `Pharmacie/Models/SaleCreateViewModel.cs`
- `Pharmacie/Views/Sales/Create.cshtml`
- `Pharmacie/Views/Sales/Index.cshtml`
- `Pharmacie/Views/Sales/Details.cshtml`

---

### 1.2 Modèles

**`Sale`** : `Id`, `SoldAt`, `Notes`, `UserId` (compte qui valide), `VendeurId` + `Vendeur`, `PaymentMethod`, `Lines`.

**`SaleLine`** : `ProductId`, `Quantity`, `UnitPrice` (décimal, persisté au moment de la vente).

**`SaleCreateViewModel`** : `SoldAt` (défaut `DateTime.Now`), `Notes`, `PaymentMethod` (défaut `Especes`), `VendeurId` (requis côté contrôleur), `Lines` → `SaleLineSlotViewModel` avec seulement **`ProductId`** et **`Quantity`** (pas de prix ni remise postés).

**`PaymentMethod`** (enum) : **Espèces**, **Wave**, **Orange Money** uniquement — pas Chèque / Virement / Autre.

---

### 1.3 `Index.cshtml`

- En-tête + bouton « Nouvelle vente ».
- Filtres date + utilisateur (liste dérivée des `UserId` déjà présents sur des ventes).
- Tableau : N°, date, nb lignes, **total** (`Σ Qté × Prix unit.`), badge paiement, vendeur, « Enregistré par », lien Détail.
- Pagination via `_Pagination`.
- Script : tooltips Bootstrap uniquement.

---

### 1.4 `Details.cshtml`

- En-tête : date, enregistré par, moyen de paiement.
- Notes optionnelles.
- Tableau lignes : produit, prix unit., qté, sous-total ; **total** en pied.
- Lien mouvements de stock, retour liste, export CSV.

**Écart :** pas d’affichage du **vendeur** sur cette vue (alors qu’`Index` l’affiche).

---

## 2. Interface actuelle — `Create.cshtml` (détail)

### Structure du formulaire

- Une colonne (`col-lg-10`), carte « Saisie de la vente ».
- **`form asp-action="Create"`** (POST implicite, validation client via partial).
- **En-tête** : `SoldAt` (`datetime-local`), `Notes` (texte libre).
- **Lignes** : tableau fixe de **`Model.Lines.Count`** lignes (initialisé à **8** au GET).
- **Paiement** : `<select asp-for="PaymentMethod">` avec `GetEnumSelectList<PaymentMethod>()`.
- **Vendeur** : `<select asp-for="VendeurId">` depuis `ViewBag.Vendeurs` (actifs, nom + couleur ticket).
- Actions : **« Valider la vente »** (`btn-primary`) + **« Annuler »** → `Index`.

### Ajout des produits

- **Une ligne = un `<select>` TomSelect** par index (`Lines[i].ProductId`).
- Recherche AJAX : `GET /Products/Search?term=…` (min. 2 caractères), max 25 résultats.
- Libellé : `CIP — nom (stock: n)` ; champs JSON : `value`, `text`, `salePrice`, `purchasePrice`, `stockQuantity`.
- Pas d’« ajouter une ligne » dynamique : **8 slots fixes** ; lignes avec `ProductId == 0` ou `Quantity == 0` ignorées au POST.

### Quantité

- `input type="number" min="0"` par ligne (`Lines[i].Quantity`).
- Pas de boutons +/- ; pas de contrôle stock côté client.

### Total en temps réel ?

- **Non.** Aucun affichage sous-total / total dans la page.

### Remise ?

- **Non** (ni ligne, ni globale, ni champs modèle).

### Paiement

- Liste déroulante enum (3 valeurs), pas de boutons visuels.

### Bouton de validation

- Submit classique ; en cas de succès → `Details` ; erreurs métier (`SaleService`) → `ModelState` message global.

### JavaScript

1. `_ValidationScriptsPartial` (jQuery unobtrusive).
2. **TomSelect** sur `[data-tomselect-product]` :
   - chargement distant, throttle 300 ms ;
   - `onChange` tente de remplir un champ **`UnitPrice`** via `selectName.replace('ProductId', 'UnitPrice')` — **ce champ n’existe pas dans le formulaire** (code mort / vestige).
3. Antiforgery : tag helper `form` POST (pas de `@Html.AntiForgeryToken()` explicite).

---

## 3. Logique métier — `SaleService.RecordSaleAsync`

### Création d’une vente

1. Transaction EF.
2. Crée `Sale` : `SoldAt`, `Notes`, `UserId`, `PaymentMethod`.
3. Pour chaque `(productId, quantity)` :
   - Produit doit exister et `IsActive`.
   - Vérifie stock **lots non expirés** à la date `soldAt.Date`.
   - Ajoute `SaleLine` avec **`UnitPrice = product.SalePrice`** (prix catalogue au moment T, **pas** saisi par l’UI).
   - Décrémente lots en **FIFO** : `ExpirationDate` croissant, puis `Id` ; met à jour `batch.Quantity`, `product.StockQuantity`, crée `StockMovement` (Sortie, lié à la vente).
4. `SaveChanges` + commit ; retour `(Ok, Error, SaleId)`.

### FIFO

- Lots avec `Quantity > 0` et `ExpirationDate >= refDate`.
- Consommation par ordre de péremption ; plusieurs mouvements si plusieurs lots.

### Vendeur

- **Pas géré dans `SaleService`.**
- Le contrôleur, après succès, recharge la vente et set `VendeurId` + `SaveChanges`.

### Moyen de paiement

- Paramètre `paymentMethod` (défaut `Especes`) ; stocké sur `Sale` à la création.

### Validations

| Où | Quoi |
|----|------|
| Service | Au moins une ligne ; produit actif ; stock non expiré suffisant ; allocation lots cohérente |
| Contrôleur | Au moins une ligne `(ProductId>0, Qty>0)` ; `VendeurId` obligatoire et vendeur actif en base |
| Vue / VM | `VendeurId` `[Required]` sur le VM ; slots quantité `[Range(0, …)]` |

### Stock insuffisant

- Rollback transaction.
- Message : stock insuffisant (lots non expirés), détail nom produit, dispo vs demandé ; mention optionnelle du stock sur lots **expirés** non vendables.
- Tests (`SaleServiceTests`) : refus si seuls lots expirés ; refus si quantité > stock non expiré ; acceptation FIFO.

---

## 4. Ce qui change vs ce qui reste

### À garder tel quel (recommandé)

- **`SaleService`** (FIFO, transactions, messages d’erreur) — contrat testé (4 tests dédiés).
- **`SalesController`** flux POST : filtrer lignes → `RecordSaleAsync` → `VendeurId` → redirect `Details`.
- **`Products/Search`** pour autocomplétion nom/CIP.
- Entités **`Sale` / `SaleLine`** pour l’historique, rapports, dashboard.
- **`Index` / `Details` / `DetailsCsv`** (éventuellement reskin plus tard).

### À modifier (UI / VM / contrôleur, pas le service si contrainte stricte)

- **`Create.cshtml`** : layout 70/30, panier dynamique, résumé temps réel, boutons paiement.
- **`SaleCreateViewModel` / slots** : liste dynamique (plus seulement 8 lignes fixes).
- **Alignement mockup ↔ backend** : le POST ne porte **que** `ProductId` + `Quantity` ; prix affiché ≠ prix enregistré si modification UI sans évolution du service.

### À ajouter pour coller au cahier des charges cible

| Besoin UI | État actuel | Impact si `SaleService` inchangé |
|-----------|-------------|----------------------------------|
| Prix modifiable par ligne | Prix = `SalePrice` en service | Affichage seulement, ou extension POST + `RecordSaleAsync` |
| Remise % ligne / globale | Absent du schéma | Calcul UI seulement, ou colonnes + migrations |
| Paiement Chèque / Virement / Autre | Enum à 3 valeurs | Migration + enum + Dashboard/rapports |
| Barre recherche unique + panier | 8 TomSelect | JS + champs cachés ou modèle enrichi |
| Vendeur à gauche | Déjà en bas du formulaire | Déplacement pur |
| Stock temps réel | `stockQuantity` dans Search | Alertes client ; vérité au submit = service |

---

## 5. Architecture proposée (nouvelle UI, `SaleService` intact)

### Principe

- **Contrat serveur inchangé** : `RecordSaleAsync(soldAt, notes, List<(ProductId, Quantity)>, userId, paymentMethod)`.
- **Nouvelle `Create.cshtml`** (+ CSS/JS dédiés) : état panier côté client, un seul POST final identique au contrôleur actuel.

### Layout cible (cahier des charges)

**Gauche (~70 %)**

- Barre de recherche produit (nom ou CIP).
- Liste panier : Nom | Prix (modifiable*) | − Qté + | Total ligne | Remise % | Poubelle.
- Vendeur sélectionnable.

**Droite (~30 %)**

- Résumé temps réel : nb articles, sous-total, remise globale %, total TTC.
- Boutons paiement : Espèces | Wave | Orange Money | Chèque | Virement | Autre**.
- « Valider la vente » (grand, vert), « Annuler » (secondaire).

\* Prix modifiable et remises : voir tableau section 4 si service inchangé.  
\*\* Chèque / Virement / Autre : nécessitent extension `PaymentMethod` + migration.

### Schéma

```
┌─────────────────────────────────────────────┬──────────────────┐
│ 70% Panier                                  │ 30% Résumé       │
│ [ Recherche produit (TomSelect ou input) ]  │ Nb articles      │
│ Tableau lignes (généré en JS)               │ Sous-total (calc)│
│  - champs cachés Lines[i].ProductId/Qty     │ Remise % (UI*)   │
│  - affichage prix = salePrice (lecture)     │ Total TTC (calc) │
│ [ Vendeur select ]                          │ [Btns paiement]  │
│                                             │ [Valider][Annuler]│
└─────────────────────────────────────────────┴──────────────────┘
```

### Flux technique

1. **GET `Create`** : VM minimal ; `ViewBag.Vendeurs`.
2. **Recherche** : réutiliser `Products/Search` ; à la sélection, ajouter/fusionner ligne panier JS.
3. **Submit** : champs cachés `Lines[i].ProductId`, `Lines[i].Quantity` (+ `SoldAt`, `Notes`, `PaymentMethod`, `VendeurId`).
4. **Résumé** : calcul client `salePrice × qty` depuis Search.
5. **Paiement** : boutons → hidden/radio (valeurs enum existantes tant que non étendu).
6. **Mobile** : empiler panier puis résumé sticky.

### Fichiers touchés (phase implémentation)

| Fichier | Nature |
|---------|--------|
| `Views/Sales/Create.cshtml` | Refonte majeure |
| `SaleCreateViewModel.cs` | Slots dynamiques |
| `wwwroot/css` / `js` | POS |
| `SalesController.Create` GET | Moins de slots fixes |

**Phase 1 sans toucher :** `SaleService.cs`, migrations, tests existants.

### Évolutions ultérieures (hors « service intact »)

- `RecordSaleAsync` avec `UnitPrice` par ligne.
- Remises en base (`DiscountPercent`, etc.).
- Enum paiement élargi.

---

## 6. Risques identifiés

1. **Model binding** : indices `Lines[i]` dynamiques ; doublons `ProductId` = plusieurs lignes / sorties FIFO distinctes.
2. **Prix / remises** : écart ticket affiché vs enregistré sans évolution service.
3. **Stock** : UI peut afficher stock obsolète entre recherche et submit.
4. **`VendeurId` après commit vente** : vente déjà persistée si échec rare sur second `SaveChanges`.
5. **JS / TomSelect** : plus de logique client ; conserver antiforgery et rôles.
6. **Tests** : pas de tests intégration `SalesController` — régression manuelle/E2E.
7. **Reporting** : remises futures à définir (réduire `UnitPrice` ? ligne négative ? champ dédié ?).

---

## 7. Synthèse en 5 points

1. **Structure** : MVC classique ; VM à 8 lignes fixes ; saisie = `ProductId` + `Qty` seulement.
2. **Métier** : prix catalogue figé sur ligne, FIFO lots, `UserId` = enregistrement, vendeur = patch post-vente, 3 moyens de paiement.
3. **Changements** : surtout UI/panier ; prix modifiable, remises, 6 paiements = écart fonctionnel explicite.
4. **Architecture** : POS 70/30, JS + hidden fields, même POST `Create`, `Products/Search` conservé.
5. **Risques** : binding, doublons produit, prix affiché/enregistré, stock concurrent, extensions reporting.
