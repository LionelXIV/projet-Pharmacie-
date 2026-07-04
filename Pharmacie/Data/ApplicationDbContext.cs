using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Models;

namespace Pharmacie.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientPrescription> PatientPrescriptions => Set<PatientPrescription>();
    public DbSet<PatientTreatmentReminder> PatientTreatmentReminders => Set<PatientTreatmentReminder>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportLine> ImportLines => Set<ImportLine>();
    public DbSet<ImportAnomaly> ImportAnomalies => Set<ImportAnomaly>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(entity =>
        {
            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(p => p.ProductType)
                .HasDefaultValue(ProductType.Inconnu);

            entity.HasIndex(p => p.Cip)
                .IsUnique()
                .HasFilter("[Cip] IS NOT NULL AND [Cip] <> ''");
        });

        builder.Entity<ProductBatch>(entity =>
        {
            entity.ToTable("ProductBatches");
            entity.Property(b => b.ExpirationDate).HasColumnType("date");
            entity.HasOne(b => b.Product)
                .WithMany(p => p.Batches)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.SourceImportLine)
                .WithMany()
                .HasForeignKey(b => b.SourceImportLineId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StockMovement>(entity =>
        {
            entity.HasOne(m => m.Product)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Batch)
                .WithMany(b => b.StockMovements)
                .HasForeignKey(m => m.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Sale)
                .WithMany()
                .HasForeignKey(m => m.SaleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SaleLine>(entity =>
        {
            entity.HasOne(l => l.Sale)
                .WithMany(s => s.Lines)
                .HasForeignKey(l => l.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasOne(o => o.Supplier)
                .WithMany()
                .HasForeignKey(o => o.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.HasOne(l => l.PurchaseOrder)
                .WithMany(o => o.Lines)
                .HasForeignKey(l => l.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceipt>(entity =>
        {
            entity.HasOne(r => r.PurchaseOrder)
                .WithMany(o => o.Receipts)
                .HasForeignKey(r => r.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.Property(l => l.ExpirationDate).HasColumnType("date");
            entity.HasOne(l => l.GoodsReceipt)
                .WithMany(r => r.Lines)
                .HasForeignKey(l => l.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.PurchaseOrderLine)
                .WithMany()
                .HasForeignKey(l => l.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Patient>(entity =>
        {
            entity.Property(p => p.DateOfBirth).HasColumnType("date");
        });

        builder.Entity<PatientPrescription>(entity =>
        {
            entity.Property(p => p.PrescribedAt).HasColumnType("date");
            entity.Property(p => p.RenewalDate).HasColumnType("date");
            entity.HasOne(p => p.Patient)
                .WithMany(pt => pt.Prescriptions)
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PatientTreatmentReminder>(entity =>
        {
            entity.Property(r => r.ReminderDate).HasColumnType("date");
            entity.HasOne(r => r.Patient)
                .WithMany(p => p.TreatmentReminders)
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("ImportBatches");
            entity.HasMany(b => b.Lines)
                .WithOne(l => l.ImportBatch)
                .HasForeignKey(l => l.ImportBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(b => b.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(b => b.ConfirmedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ImportLine>(entity =>
        {
            entity.ToTable("ImportLines");
            entity.Property(l => l.RawPxFab).HasColumnType("decimal(18,2)");
            entity.Property(l => l.RawPph).HasColumnType("decimal(18,2)");

            entity.HasIndex(l => l.ImportBatchId);
            entity.HasIndex(l => l.ResolvedAction);

            entity.HasOne(l => l.MatchedProduct)
                .WithMany()
                .HasForeignKey(l => l.MatchedProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.CreatedBatch)
                .WithMany()
                .HasForeignKey(l => l.CreatedBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(l => l.Anomalies)
                .WithOne(a => a.ImportLine)
                .HasForeignKey(a => a.ImportLineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ImportAnomaly>(entity =>
        {
            entity.ToTable("ImportAnomalies");
            entity.Property(a => a.ResolvedByUser)
                .HasDefaultValue(false);
        });
    }
}
