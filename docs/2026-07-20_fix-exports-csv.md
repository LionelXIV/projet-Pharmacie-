# Fix — Exports CSV (encodage BOM, séparateur Excel FR)

**Date :** 2026-07-20  
**Commit :** `fix: improve CSV exports encoding, separator and PaymentMethod display`

## Audit — exports trouvés (tous CSV, mémoire)

| Action | Fichier |
|--------|---------|
| Reports : StockStatus, NearExpiration, ExpiredProducts, SalesHistory, StockMovementsHistory | `rapport-*.csv` |
| Products IndexCsv | `export-catalogue-produits_*.csv` |
| Suppliers IndexCsv | `export-fournisseurs_*.csv` |
| Sales DetailsCsv | `vente-{id}-lignes_*.csv` |
| Patients ×4 | patients / fiche / ordonnances / rappels |

Pas d’export `.xlsx` natif (ClosedXML = import uniquement).

## Problèmes

| Point | Avant | Après |
|-------|--------|--------|
| BOM UTF-8 | **Bug** : `GetBytes` n’émettait pas le BOM | Préambule `EF BB BF` concaténé |
| Séparateur | `,` (Excel FR casse les colonnes) | `;` + ligne `sep=;` |
| PaymentMethod | Déjà en français | Conservé (Espèces / Wave / Orange Money) |
| Mémoire / Azure | Déjà `byte[]` | Inchangé |
| Montants CSV | `N0` invariant (`1,234`) | Entier sans milliers |

## Vérifications manuelles

- BOM présent sur SalesHistory, Stock, vente #9, produits, fournisseurs
- Colonnes séparées par `;` ; accents UTF-8 ; Wave / Espèces lisibles
- Build 0 erreur / 0 warning — **23/23** tests
