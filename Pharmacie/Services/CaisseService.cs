using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class CaisseService
{
    public const decimal SeuilDepotFcfa = 200_000m;

    private readonly ApplicationDbContext _db;

    public CaisseService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Error, int SessionId)> OuvrirCaisseAsync(
        int numeroCaisse,
        decimal fondDepart,
        string userId)
    {
        if (numeroCaisse is not (1 or 2))
            return (false, "Numéro de caisse invalide (1 = Matin, 2 = Soir).", 0);

        if (fondDepart < 0)
            return (false, "Le fond de départ ne peut pas être négatif.", 0);

        // Vérifier uniquement qu'une session OUVERTE n'existe pas déjà pour cette caisse
        var sessionOuverte = await _db.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.NumeroCaisse == numeroCaisse
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (sessionOuverte != null)
            return (false, $"La {sessionOuverte.NomCaisse} est déjà ouverte.", 0);

        var sessionUser = await _db.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.CaissierUserId == userId
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (sessionUser != null)
            return (false, $"Vous avez déjà une session ouverte ({sessionUser.NomCaisse}). Fermez-la avant d'en ouvrir une autre.", 0);

        // Nouvelle session même si des sessions fermées existent (réouverture)
        var session = new SessionCaisse
        {
            NumeroCaisse = numeroCaisse,
            DateSession = DateTime.Today,
            HeureOuverture = DateTime.Now,
            FondDepart = fondDepart,
            CaissierUserId = userId,
            Statut = SessionCaisseStatut.Ouverte
        };

        _db.SessionCaisses.Add(session);
        await _db.SaveChangesAsync();
        return (true, "", session.Id);
    }

    public async Task<(bool Success, string Error)> FermerCaisseAsync(
        int sessionId,
        BilletageDetail billetage,
        string? notes,
        string userId,
        bool isAdminOrPharmacien = false)
    {
        var session = await _db.SessionCaisses.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null)
            return (false, "Session introuvable.");

        if (!isAdminOrPharmacien && session.CaissierUserId != userId)
            return (false, "Vous ne pouvez fermer que votre propre caisse.");

        if (session.Statut == SessionCaisseStatut.Fermee)
            return (false, "Cette caisse est déjà fermée.");

        session.HeureFermeture = DateTime.Now;
        session.BilletageTotal = billetage.Total;
        session.BilletageJson = JsonSerializer.Serialize(billetage);
        session.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        session.Statut = SessionCaisseStatut.Fermee;

        await _db.SaveChangesAsync();
        return (true, "");
    }

    public async Task<SessionCaisse?> GetSessionOuverteAsync(string userId)
    {
        return await _db.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.CaissierUserId == userId
                && s.Statut == SessionCaisseStatut.Ouverte);
    }

    public async Task<SessionCaisse?> GetSessionDuJourAsync(int numeroCaisse, DateTime? date = null)
    {
        var d = (date ?? DateTime.Today).Date;
        return await _db.SessionCaisses
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .Where(s => s.NumeroCaisse == numeroCaisse && s.DateSession == d)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public async Task LierVenteAsync(int sessionId, int saleId)
    {
        var exists = await _db.VenteCaisses
            .AnyAsync(v => v.SessionCaisseId == sessionId && v.SaleId == saleId);
        if (exists) return;

        _db.VenteCaisses.Add(new VenteCaisse
        {
            SessionCaisseId = sessionId,
            SaleId = saleId
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Solde théorique espèces : fond + CA espèces − dépôts déjà effectués.
    /// </summary>
    public async Task<decimal> GetSoldeTheoriqueAsync(int sessionId)
    {
        var session = await _db.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return 0;

        var caEspeces = session.Ventes
            .Select(v => v.Sale)
            .Where(s => s != null && s.PaymentMethod == PaymentMethod.Especes)
            .Cast<Sale>()
            .Sum(CalculerTotalSale);

        var totalDepots = await _db.DepotCaisses
            .Where(d => d.SessionCaisseId == sessionId)
            .SumAsync(d => (decimal?)d.MontantDepose) ?? 0;

        return session.FondDepart + caEspeces - totalDepots;
    }

    public async Task<(bool Success, string Error, int DepotId)> EnregistrerDepotAsync(
        int sessionId,
        decimal montant,
        DepotCaisseType type,
        string userId,
        bool isAdminOrPharmacien = false)
    {
        var session = await _db.SessionCaisses.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null)
            return (false, "Session introuvable.", 0);

        if (session.Statut != SessionCaisseStatut.Ouverte)
            return (false, "La caisse est fermée — dépôt impossible.", 0);

        if (!isAdminOrPharmacien && session.CaissierUserId != userId)
            return (false, "Vous ne pouvez déposer que depuis votre propre caisse.", 0);

        if (montant <= 0)
            return (false, "Le montant doit être supérieur à 0.", 0);

        var soldeAvant = await GetSoldeTheoriqueAsync(sessionId);
        if (montant > soldeAvant)
            return (false, $"Montant supérieur au solde disponible ({soldeAvant:N0} FCFA).", 0);

        var depot = new DepotCaisse
        {
            SessionCaisseId = sessionId,
            HeureDepot = DateTime.Now,
            MontantDepose = montant,
            SoldeAvantDepot = soldeAvant,
            SoldeApresDepot = soldeAvant - montant,
            Type = type,
            EffectueParUserId = userId
        };

        _db.DepotCaisses.Add(depot);
        await _db.SaveChangesAsync();
        return (true, "", depot.Id);
    }

    public static decimal CalculerSoldeTheorique(
        SessionCaisse session,
        IEnumerable<DepotCaisse>? depots = null)
    {
        var sales = session.Ventes.Select(v => v.Sale).Where(s => s != null).Cast<Sale>().ToList();
        var caEspeces = sales
            .Where(s => s.PaymentMethod == PaymentMethod.Especes)
            .Sum(CalculerTotalSale);
        var totalDepots = (depots ?? session.Depots).Sum(d => d.MontantDepose);
        return session.FondDepart + caEspeces - totalDepots;
    }

    public static decimal CalculerTotalSale(Sale sale)
    {
        return sale.Lines.Sum(LineTotal);
    }

    public static decimal LineTotal(SaleLine l)
    {
        var baseAmount = l.UnitPrice * l.Quantity;
        if (l.DiscountType == "percent" && l.DiscountPercent > 0)
            return baseAmount * (1 - l.DiscountPercent / 100m);
        if (l.DiscountType == "amount" && l.DiscountAmount > 0)
            return Math.Max(0, baseAmount - l.DiscountAmount);
        return baseAmount;
    }
}
