# Feat — Marge brute et panier moyen (Dashboard / Finances)

**Date :** 2026-07-20  
**Commit :** `feat: add daily margin and average basket to dashboard (Pharmacien/Admin only)`

## Audit préalable

- Dashboard calculait déjà `SalesTodayCount` / `SalesTodayTotal` (2 requêtes SQL), sans `Product`.
- KPI UI : stock, alertes, commandes, ventes du jour, graphiques 30 j — **pas de finances**.
- Accès dashboard limité à Admin / Pharmacien / GestionnaireStock (Caissier exclu).

## Implémentation

- Une seule charge `todaySales` (+ lignes + produits) → CA, marge, panier moyen.
- Section « Finances du jour » (marge, panier moyen, taux) si `CanAccessFinances`.
- Page `Dashboard/Finances` (30 j + répartition Espèces/Wave/Orange Money), `[Authorize(Roles = FinancesAccess)]`.
- Dashboard ouvert à tous les rôles métier ; rapports inchangés (Admin/Pharmacien/GestionnaireStock).

## Vérifications

| Rôle | Section + lien Finances | Page Finances |
|------|-------------------------|---------------|
| Administrateur | visible | 200 |
| Pharmacien | visible | 200 |
| Caissier | absents | redirigé (interdit) |

Calcul jour (2 ventes) : marge **12 FCFA**, panier moyen **9 FCFA**, taux **64,4 %**.  
Build 0 erreur / 0 warning — tests **23/23**.
