-- =============================================================================
-- Script de remise à zéro des données de production
-- Pharmacie Saint Jean Paul II PNR
-- Fournisseur : SQL Server (Azure)
--
-- CONSERVE :
--   AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, etc.
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
-- ATTENTION : action IRRÉVERSIBLE. Faire une sauvegarde avant exécution.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

BEGIN TRY
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

    -- Réinitialiser les compteurs d'identité (prochains Id = 1)
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'ImportAnomalies')
        DBCC CHECKIDENT ('ImportAnomalies', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'ImportLines')
        DBCC CHECKIDENT ('ImportLines', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'ImportBatches')
        DBCC CHECKIDENT ('ImportBatches', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'UserActivityReports')
        DBCC CHECKIDENT ('UserActivityReports', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'PatientTreatmentReminders')
        DBCC CHECKIDENT ('PatientTreatmentReminders', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'PatientPrescriptions')
        DBCC CHECKIDENT ('PatientPrescriptions', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'Patients')
        DBCC CHECKIDENT ('Patients', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'SaleLines')
        DBCC CHECKIDENT ('SaleLines', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'Sales')
        DBCC CHECKIDENT ('Sales', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'GoodsReceiptLines')
        DBCC CHECKIDENT ('GoodsReceiptLines', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'GoodsReceipts')
        DBCC CHECKIDENT ('GoodsReceipts', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'PurchaseOrderLines')
        DBCC CHECKIDENT ('PurchaseOrderLines', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'PurchaseOrders')
        DBCC CHECKIDENT ('PurchaseOrders', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'StockMovements')
        DBCC CHECKIDENT ('StockMovements', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'ProductBatches')
        DBCC CHECKIDENT ('ProductBatches', RESEED, 0);
    IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE OBJECT_NAME(object_id) = 'Products')
        DBCC CHECKIDENT ('Products', RESEED, 0);

    COMMIT TRANSACTION;
    PRINT 'Remise à zéro terminée. Utilisateurs, rôles, vendeurs, catégories et fournisseurs conservés.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity int = ERROR_SEVERITY();
    RAISERROR(@ErrMsg, @ErrSeverity, 1);
END CATCH;
