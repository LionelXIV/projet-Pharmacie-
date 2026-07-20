# Feat — Moyen de paiement sur les ventes (Espèces / Wave / Orange Money)

**Date :** 2026-07-20  
**Commit prévu :** `feat: add payment method (Especes/Wave/OrangeMoney) to sales`

## Demande client

Saisie manuelle du moyen de paiement par vente (pas d’intégration Wave / Orange Money API), pour distinguer les encaissements dans les rapports de caisse.

## Implémentation

| Étape | Détail |
|-------|--------|
| Enum | `Models/PaymentMethod.cs` + `[Display]` FR ; helper `PaymentMethodDisplay` (libellé + CSS badge) |
| Entité | `Sale.PaymentMethod` défaut `Especes` |
| Migration | `Step10_AddPaymentMethodToSales` — colonne `int NOT NULL DEFAULT 0` |
| Create | `SaleCreateViewModel` + select `GetEnumSelectList` avant validation |
| Service | `SaleService.RecordSaleAsync(..., PaymentMethod paymentMethod = Especes)` — mapping seul |
| Index | Colonne « Paiement » + badges (gris / bleu / orange) |
| Details | Affichage « Moyen de paiement : … » |
| Rapports | `SalesHistory` + CSV colonne « Moyen de paiement » |

## Vérifications

1. Migration appliquée localement (LocalDB `PharmacieDb`)
2. Create : options **Espèces**, **Wave**, **Orange Money**
3. Vente Wave (#9) enregistrée `PaymentMethod = 1`
4. Index : badge bleu « Wave »
5. Details : « Moyen de paiement : Wave »
6. CSV : en-tête `Moyen de paiement`, ligne `...,Wave`
7. `dotnet build` → 0 erreur, 0 warning ; `dotnet test` → **23/23** verts

## Non modifié (consigne)

FIFO `SaleService`, `InventoryService`, `PurchaseService`, migrations antérieures, tests existants (défaut Especes).
