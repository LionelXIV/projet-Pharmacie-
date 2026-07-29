-- =============================================================================
-- Scripts/reset-data.sql
-- Remise à zéro des données métier — SQL Server / Azure SQL
-- Pharmacie Saint Jean Paul II PNR
--
-- CONSERVE :
--   AspNetUsers, AspNetRoles, AspNetUserRoles (+ autres tables Identity)
--   Categories, Suppliers, Vendeurs
--
-- SUPPRIME (ordre FK) :
--   ImportAnomalies → ImportLines → ImportBatches
--   UserActivityReports
--   PatientTreatmentReminders → PatientPrescriptions → Patients
--   SaleLines → Sales
--   GoodsReceiptLines → GoodsReceipts
--   PurchaseOrderLines → PurchaseOrders
--   StockMovements → ProductBatches → Products
--
-- Exemple (sqlcmd) :
--   sqlcmd -S tcp:....database.windows.net,1433 -d pharmacie-db -U pharmacieadmin -P "..." -i Scripts/reset-data.sql
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

BEGIN TRY
    -- Couper les FK croisees avant DELETE
    UPDATE [ProductBatches] SET [SourceImportLineId] = NULL WHERE [SourceImportLineId] IS NOT NULL;
    UPDATE [ImportLines] SET [MatchedProductId] = NULL, [CreatedBatchId] = NULL
        WHERE [MatchedProductId] IS NOT NULL OR [CreatedBatchId] IS NOT NULL;
    UPDATE [StockMovements] SET [SaleId] = NULL WHERE [SaleId] IS NOT NULL;

    DELETE FROM [ImportAnomalies];
    DELETE FROM [ImportLines];
    DELETE FROM [ImportBatches];
    DELETE FROM [UserActivityReports];
    DELETE FROM [PatientTreatmentReminders];
    DELETE FROM [PatientPrescriptions];
    DELETE FROM [Patients];
    DELETE FROM [SaleLines];
    DELETE FROM [Sales];
    DELETE FROM [GoodsReceiptLines];
    DELETE FROM [GoodsReceipts];
    DELETE FROM [PurchaseOrderLines];
    DELETE FROM [PurchaseOrders];
    DELETE FROM [StockMovements];
    DELETE FROM [ProductBatches];
    DELETE FROM [Products];

    COMMIT TRANSACTION;
    PRINT 'OK: donnees metier supprimees. Utilisateurs, roles, vendeurs, categories et fournisseurs conserves.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity int = ERROR_SEVERITY();
    RAISERROR(@ErrMsg, @ErrSeverity, 1);
END CATCH;
