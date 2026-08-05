using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class CaisseService
{
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

        var today = DateTime.Today;

        var dejaOuverte = await _db.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.NumeroCaisse == numeroCaisse
                && s.DateSession == today
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (dejaOuverte != null)
            return (false, $"La {dejaOuverte.NomCaisse} est déjà ouverte.", 0);

        var sessionUser = await _db.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.CaissierUserId == userId
                && s.DateSession == today
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (sessionUser != null)
            return (false, $"Vous avez déjà une session ouverte ({sessionUser.NomCaisse}). Fermez-la avant d'en ouvrir une autre.", 0);

        var session = new SessionCaisse
        {
            NumeroCaisse = numeroCaisse,
            DateSession = today,
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
        var today = DateTime.Today;
        return await _db.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.CaissierUserId == userId
                && s.Statut == SessionCaisseStatut.Ouverte
                && s.DateSession == today);
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
