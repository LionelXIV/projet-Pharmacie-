# Brief de mise à jour — Projet Pharmacie (20 juillet 2026)

À coller pour mettre à jour Claude / une autre IA.

## Contexte client / prod

- **Projet :** ASP.NET Core 9 MVC « Pharmacie »
- **Client :** Pharmacie Saint Jean Paul II PNR, Dakar, Sénégal
- **Prod Azure :** https://pharmacie-saintjeanpaul-c3gscbg7eke9gfdu.francecentral-01.azurewebsites.net
- **Repo local :** `c:\Users\pc\Downloads\projet pharmacie`
- **Branche :** `main`
- Client a signalé des erreurs après déploiement. Audit fait + corrections UI/CI locales. **Pas forcément commit/push** au moment du brief.

## Stack

- ASP.NET Core 9 MVC + Identity + EF Core SQL Server
- Rôles : Administrateur, Pharmacien, Assistant, GestionnaireStock, Caissier
- CI/CD GitHub Actions → Azure App Service
- Au démarrage (y compris Production) : `MigrateAsync()` + seed rôles/admin ; DemoDataSeed uniquement en Development

## Inventaire

- **18 contrôleurs**, **8 services**, **17 DbSet** métier, **66 vues**
- **Tests :** 23/23 verts (import, ventes FIFO, réception)
- Build Release : 0 erreur, 0 avertissement

## Module import — dettes documentées

Voir `README_ModuleImport.md` : expiration +2 ans, ProductType Inconnu, QTEFACT/PPH non validés client, volume prod non testé.

## Corrections locales non commités (session audit follow-up)

Voir `docs/2026-07-20_inventaire-anomalies-client.txt` :

- Privacy : identité client
- Layout : marque St Jean Paul II
- Sales Index : icône euro → cash-coin
- Accueil actif seulement sur Home/Index
- CI : `dotnet test` avant publish

## Diagnostic ventes (session suivante)

Voir `docs/2026-07-20_diagnostic-bouton-validation-ventes.md`  
Cause probable n°1 : Flatpickr `altInput` sur `datetime-local` (SoldAt) bloque le submit HTML5 silencieusement.

## Fichiers sensibles bugs métier

- `SaleService`, `InventoryService`, `PurchaseService`, `ProductImportService`
- `Program.cs`, `web.config`, workflow Azure

## Règles

- Ne pas changer la logique import sans validation client
- Ne pas commit/push sans demande explicite
- Enregistrer chaque réponse agent dans `docs/`
