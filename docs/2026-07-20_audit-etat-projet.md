# État du projet Pharmacie — Audit de référence (20 juillet 2026)

**Contexte :** déploiement Azure ; client signale erreurs. Audit lecture seule initial, puis corrections ciblées hors plan.

## Program.cs

- Stack MVC + Identity + EF SQL Server
- Policies RequireAdministrator, ProductSearch
- Services DI : Inventory, Purchase, Sale, ExcelReader, ImportValidation, ImportMatching, ProductImport
- MigrateAsync + seed au démarrage (prod incluse)
- DemoDataSeed Development only

## Contrôleurs (18)

Home, Dashboard, Products, Categories, Suppliers, Batches, StockMovements, PurchaseOrders, GoodsReceipts, Sales, Alerts, Reports, Patients, PatientPrescriptions, PatientReminders, PatientRemindersAlerts, ProductImports, AdminUsers

## Services (8)

Inventory, Purchase, Sale, ExcelReader, ImportValidation, ImportMatching, ProductImport, UserDisplayResolver (statique)

## Entités EF (17 DbSet)

Category, Supplier, Product, ProductBatch, StockMovement, PurchaseOrder, PurchaseOrderLine, GoodsReceipt, GoodsReceiptLine, Sale, SaleLine, Patient, PatientPrescription, PatientTreatmentReminder, ImportBatch, ImportLine, ImportAnomaly (+ Identity)

## Vues / Tests / CI

- **66** `.cshtml`
- **23/23** tests verts
- Workflow Azure : build + publish + deploy (étape test ajoutée localement ensuite)

## Dettes import (README_ModuleImport.md)

1. Expiration provisoire +2 ans
2. ProductType Inconnu
3. Volume réel non validé
4. Catégorie/fournisseur par défaut
5. Questions ouvertes QTEFACT / PX_FAB-PPH

## 5 derniers commits (au moment de l’audit)

1. `3a3ab79` fix: add web.config with stdout logging enabled
2. `dc09bea` fix: enable migrations and startup in production
3. `34b0b3f` fix: add quotes around publish output path
4. `095294a` fix: correct project path in GitHub Actions workflow
5. `1b35388` Add or update the Azure App Service build and deployment workflow config
