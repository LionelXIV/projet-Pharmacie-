using Microsoft.EntityFrameworkCore;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Data;

/// <summary>
/// Données de démonstration pour captures d’écran (développement uniquement).
/// Pour réinitialiser : supprimer les patients dont le téléphone est <see cref="MarkerPhone"/>, puis redémarrer l’app.
/// </summary>
public static class DemoDataSeed
{
    /// <summary>Téléphone fictif réservé au premier patient du jeu de données (marqueur d’idempotence).</summary>
    public const string MarkerPhone = "418-555-0142";

    public static async Task SeedIfNeededAsync(
        ApplicationDbContext db,
        InventoryService inventory,
        PurchaseService purchase,
        SaleService sales,
        string? adminUserId)
    {
        if (await db.Patients.AsNoTracking().AnyAsync(p => p.Phone == MarkerPhone))
            return;

        var today = DateTime.Today;

        var categories = new[]
        {
            "Analgésiques et anti-inflammatoires",
            "Allergies et rhume",
            "Troubles digestifs",
            "Vitamines et compléments",
            "Hygiène et soins"
        };
        foreach (var name in categories)
            db.Categories.Add(new Category { Name = name });

        var suppliers = new[]
        {
            ("McKesson Canada", "Joanne Picard", "514-555-0180"),
            ("Pharmetics Québec", "Service comptes", "418-555-0166"),
            ("Sanofi consommateurs", "Info commandes", "450-555-0133"),
            ("Perrigo Canada", "Réception", "905-555-0177")
        };
        foreach (var (n, c, p) in suppliers)
            db.Suppliers.Add(new Supplier { Name = n, Contact = c, Phone = p });

        await db.SaveChangesAsync();

        int Cat(string name) => db.Categories.AsNoTracking().First(c => c.Name == name).Id;
        int Sup(string name) => db.Suppliers.AsNoTracking().First(s => s.Name == name).Id;

        var products = new[]
        {
            ("Acétaminophène extra-fort 500 mg", "acétaminophène", categories[0], suppliers[0].Item1, 4.25m, 10.49m, 18, "comprimé", "500 mg", "Allée A, tablette 2", true),
            ("Ibuprofène 200 mg", "ibuprofène", categories[0], suppliers[3].Item1, 3.10m, 8.75m, 38, "capsule", "200 mg", "Allée A, tablette 3", true),
            ("Loratadine 10 mg", "loratadine", categories[1], suppliers[1].Item1, 2.40m, 7.25m, 8, "comprimé", "10 mg", "Allée B, face 1", true),
            ("Oméprazole comp. gastro-résistants 20 mg", "oméprazole", categories[2], suppliers[3].Item1, 5.80m, 14.99m, 6, "comprimé", "20 mg", "Allée C", true),
            ("Vitamine D3 1000 UI", "cholécalciférol", categories[3], suppliers[2].Item1, 6.50m, 12.00m, 10, "capsule", "1000 UI", "Comptoir vitamines", true),
            ("Sirop toux sèche (menthol)", "dextrométhorphane", categories[1], suppliers[0].Item1, 4.00m, 11.49m, 5, "sirop", "15 ml / 5 ml", "Allée B", true),
            ("Crème mains réparatrice 100 ml", null, categories[4], suppliers[1].Item1, 3.20m, 8.99m, 4, "crème", "—", "Près de la caisse", true),
            ("Bandages souples assortis", null, categories[4], suppliers[0].Item1, 5.00m, 13.49m, 6, "boîte", "30 unités", "Allée D", true),
            ("Gel hydroalcoolique 75 ml", "éthanol 70 %", categories[4], suppliers[1].Item1, 2.10m, 5.99m, 10, "flacon", "75 ml", "Près caisse", true)
        };

        foreach (var (com, gen, catName, supName, purch, sale, th, form, dos, loc, active) in products)
        {
            db.Products.Add(new Product
            {
                CommercialName = com,
                GenericName = gen,
                CategoryId = Cat(catName),
                SupplierId = Sup(supName),
                PurchasePrice = purch,
                SalePrice = sale,
                StockQuantity = 0,
                AlertThreshold = th,
                Form = form,
                Dosage = dos,
                Location = loc,
                IsActive = active
            });
        }

        await db.SaveChangesAsync();

        int Prod(string commercial) =>
            db.Products.AsNoTracking().First(p => p.CommercialName == commercial).Id;

        async Task Entree(string commercial, string lot, DateTime exp, int qty, string reason)
        {
            var (ok, err) = await inventory.RecordEntreeAsync(
                Prod(commercial),
                lot,
                exp,
                qty,
                reason,
                adminUserId);
            if (!ok)
                throw new InvalidOperationException($"Semis stock : {err}");
        }

        await Entree("Acétaminophène extra-fort 500 mg", "ACM-2026-A", today.AddMonths(14), 120, "Livraison régulière");
        await Entree("Acétaminophène extra-fort 500 mg", "ACM-2023-V", new DateTime(2023, 8, 1), 8, "Reliquat ancien lot (à retirer)");
        await Entree("Ibuprofène 200 mg", "IBU-2026-01", today.AddMonths(10), 42, "Commande mars");
        await Entree("Loratadine 10 mg", "LOR-25-04", today.AddMonths(8), 24, "Réception");
        await Entree("Vitamine D3 1000 UI", "VD3-2026", today.AddMonths(18), 36, "Promo printemps");
        await Entree("Sirop toux sèche (menthol)", "TOU-NEAR", today.AddDays(52), 14, "Stock printemps");
        await Entree("Sirop toux sèche (menthol)", "TOU-FAR", today.AddMonths(20), 20, "Stock printemps");
        await Entree("Crème mains réparatrice 100 ml", "CRE-01", today.AddMonths(11), 22, "Inventaire initial");
        await Entree("Bandages souples assortis", "BAN-2025", today.AddMonths(9), 40, "Réassort");

        async Task Vente(DateTime soldAt, params (string commercial, int qty)[] lines)
        {
            var payload = lines.Select(l => (Prod(l.commercial), l.qty)).ToList();
            var (ok, err, _) = await sales.RecordSaleAsync(soldAt, null, payload, adminUserId);
            if (!ok)
                throw new InvalidOperationException($"Semis vente : {err}");
        }

        await Vente(today.AddDays(-6).AddHours(9).AddMinutes(12), ("Ibuprofène 200 mg", 3), ("Loratadine 10 mg", 1));
        await Vente(today.AddDays(-3).AddHours(11).AddMinutes(5), ("Ibuprofène 200 mg", 3));
        await Vente(today.AddDays(-2).AddHours(16).AddMinutes(40), ("Acétaminophène extra-fort 500 mg", 1), ("Crème mains réparatrice 100 ml", 1));
        await Vente(today.AddHours(-3).AddMinutes(22), ("Vitamine D3 1000 UI", 1), ("Bandages souples assortis", 2));
        await Vente(today.AddHours(-1).AddMinutes(7), ("Loratadine 10 mg", 2));

        var (poOk, poErr) = await purchase.CreateOrderAsync(
            Sup("McKesson Canada"),
            today.AddDays(-4),
            "Bon de commande BC-4481, suivi courriel.",
            [(Prod("Oméprazole comp. gastro-résistants 20 mg"), 48), (Prod("Ibuprofène 200 mg"), 20)]);
        if (!poOk)
            throw new InvalidOperationException(poErr);

        var (po2Ok, po2Err) = await purchase.CreateOrderAsync(
            Sup("Pharmetics Québec"),
            today.AddDays(-1),
            "Urgence allergie saisonnière.",
            [(Prod("Loratadine 10 mg"), 30), (Prod("Vitamine D3 1000 UI"), 24)]);
        if (!po2Ok)
            throw new InvalidOperationException(po2Err);

        var order2 = await db.PurchaseOrders
            .Include(o => o.Lines)
            .OrderByDescending(o => o.Id)
            .FirstAsync();

        var lineLor = order2.Lines.First(l => l.ProductId == Prod("Loratadine 10 mg"));
        var lineVit = order2.Lines.First(l => l.ProductId == Prod("Vitamine D3 1000 UI"));

        var reception = new ReceptionFormViewModel
        {
            PurchaseOrderId = order2.Id,
            ReceivedAt = today.AddHours(-5),
            Notes = "Palette partielle — reliquat semaine prochaine.",
            Lines =
            [
                new ReceptionLineRowViewModel
                {
                    PurchaseOrderLineId = lineLor.Id,
                    ProductName = "Loratadine 10 mg",
                    QuantityOrdered = lineLor.QuantityOrdered,
                    QuantityReceivedBefore = 0,
                    QuantityReceived = 30,
                    LotNumber = "LOR-PE-26",
                    ExpirationDate = today.AddMonths(10)
                },
                new ReceptionLineRowViewModel
                {
                    PurchaseOrderLineId = lineVit.Id,
                    ProductName = "Vitamine D3 1000 UI",
                    QuantityOrdered = lineVit.QuantityOrdered,
                    QuantityReceivedBefore = 0,
                    QuantityReceived = 10,
                    LotNumber = "VD3-PE-26",
                    ExpirationDate = today.AddMonths(16)
                }
            ]
        };

        var (recOk, recErr) = await purchase.RecordReceptionAsync(order2.Id, reception, adminUserId);
        if (!recOk)
            throw new InvalidOperationException(recErr);

        db.Patients.AddRange(
            new Patient
            {
                FullName = "Mireille Arsenault",
                Phone = MarkerPhone,
                Email = "m.arsenault@videotron.ca",
                Address = "214, rue des Érables, Rimouski",
                DateOfBirth = new DateTime(1962, 3, 11),
                Notes = "Préfère les génériques lorsque l’équivalence est claire.",
                IsActive = true,
                Allergies = "Pénicilline, sulfamides",
                ChronicCondition = "Asthme léger",
                UsualTreatment = "Ventoline au besoin",
                TreatingDoctor = "Dr Martel (CLSC)"
            },
            new Patient
            {
                FullName = "Jean-Pierre Morin",
                Phone = "418-765-8831",
                Email = "jp.morin@gmail.com",
                Address = "89, avenue du Phare, Mont-Joli",
                DateOfBirth = new DateTime(1955, 7, 22),
                Notes = "",
                IsActive = true,
                Allergies = null,
                ChronicCondition = "Hypertension",
                UsualTreatment = "Ramipril 5 mg",
                TreatingDoctor = "Dr Létourneau"
            },
            new Patient
            {
                FullName = "Sofia Nguyen",
                Phone = "514-229-4402",
                Email = null,
                Address = "12, rue Saint-Germain, Québec",
                DateOfBirth = new DateTime(1991, 11, 3),
                Notes = "Étudiante — horaires irréguliers.",
                IsActive = true,
                Allergies = "Arachides (anaphylaxie)",
                ChronicCondition = null,
                UsualTreatment = null,
                TreatingDoctor = null
            },
            new Patient
            {
                FullName = "Marc Tessier",
                Phone = "418-332-9011",
                Email = "marc.tessier@uqar.ca",
                Address = "3, impasse des Merisiers, Rimouski",
                DateOfBirth = new DateTime(1978, 1, 30),
                Notes = "Rappel renouvellement insuline.",
                IsActive = true,
                Allergies = null,
                ChronicCondition = "Diabète type 2",
                UsualTreatment = "Metformine 500 mg x2",
                TreatingDoctor = "Dr H. Bouchard"
            },
            new Patient
            {
                FullName = "Line Bélanger",
                Phone = "418-551-2200",
                Email = "line.belanger@icloud.com",
                Address = "560, chemin du Fleuve, Saint-Anaclet",
                DateOfBirth = new DateTime(1944, 9, 9),
                Notes = "Fiche créée après transfert depuis une autre officine.",
                IsActive = false,
                Allergies = "Codéine (nausées)",
                ChronicCondition = "Arthrose",
                UsualTreatment = "Acétaminophène au besoin",
                TreatingDoctor = "Dr P. Gagnon"
            });

        await db.SaveChangesAsync();

        var mireille = await db.Patients.FirstAsync(p => p.Phone == MarkerPhone);
        var marc = await db.Patients.FirstAsync(p => p.FullName == "Marc Tessier");
        var jp = await db.Patients.FirstAsync(p => p.FullName == "Jean-Pierre Morin");

        db.PatientPrescriptions.AddRange(
            new PatientPrescription
            {
                PatientId = mireille.Id,
                PrescribedAt = today.AddMonths(-2),
                DoctorName = "Dr Martel",
                Content = "Ramipril 5 mg, 1 comprimé au coucher, 90 jours.",
                RenewalDate = today.AddDays(12),
                Status = PrescriptionStatus.Active
            },
            new PatientPrescription
            {
                PatientId = mireille.Id,
                PrescribedAt = today.AddMonths(-8),
                DoctorName = "Dr Martel",
                Content = "Salbutamol doseur, 1-2 bouffées PRN.",
                RenewalDate = null,
                Status = PrescriptionStatus.Archived
            },
            new PatientPrescription
            {
                PatientId = marc.Id,
                PrescribedAt = today.AddDays(-20),
                DoctorName = "Dr H. Bouchard",
                Content = "Metformine 500 mg, 1 comprimé au déjeuner et au souper.",
                RenewalDate = today.AddDays(40),
                Status = PrescriptionStatus.Active
            },
            new PatientPrescription
            {
                PatientId = jp.Id,
                PrescribedAt = today.AddMonths(-1),
                DoctorName = "Dr Létourneau",
                Content = "Suivi tension : poursuivre traitement actuel.",
                RenewalDate = today.AddMonths(2),
                Status = PrescriptionStatus.Active
            });

        db.PatientTreatmentReminders.AddRange(
            new PatientTreatmentReminder
            {
                PatientId = marc.Id,
                ReminderType = PatientReminderType.PrescriptionRenewal,
                ReminderDate = today,
                Message = "Téléphoner pour renouvellement metformine (reste ~10 jours).",
                IsDone = false
            },
            new PatientTreatmentReminder
            {
                PatientId = mireille.Id,
                ReminderType = PatientReminderType.Treatment,
                ReminderDate = today.AddDays(-1),
                Message = "Vérifier adhérence au traitement anti-hypertenseur.",
                IsDone = true
            },
            new PatientTreatmentReminder
            {
                PatientId = jp.Id,
                ReminderType = PatientReminderType.ClientCall,
                ReminderDate = today.AddDays(3),
                Message = "Rappeler résultats analyses (laboratoire).",
                IsDone = false
            });

        await db.SaveChangesAsync();
    }
}
