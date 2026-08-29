using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class SessionAutoCloseService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SessionAutoCloseService> _logger;

    public SessionAutoCloseService(
        IServiceProvider services,
        ILogger<SessionAutoCloseService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            await FermerSessionsOuvertes();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur fermeture automatique sessions (demarrage)");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var maintenant = DateTime.Now;
            var minuit = maintenant.Date.AddDays(1);
            var delai = minuit - maintenant;
            if (delai < TimeSpan.FromSeconds(1))
                delai = TimeSpan.FromSeconds(1);

            try
            {
                await Task.Delay(delai, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await FermerSessionsOuvertes();
                _logger.LogInformation("Sessions fermees automatiquement a minuit");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur fermeture automatique sessions");
            }
        }
    }

    private async Task FermerSessionsOuvertes()
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var aujourdHui = DateTime.Today;

        var sessionsOuvertes = await context.SessionCaisses
            .Where(sc =>
                sc.Statut == SessionCaisseStatut.Ouverte
                && sc.DateSession < aujourdHui)
            .ToListAsync();

        foreach (var session in sessionsOuvertes)
        {
            session.HeureFermeture = session.DateSession.AddDays(1).AddSeconds(-1);
            session.Statut = SessionCaisseStatut.Fermee;
            session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                ? "Fermee automatiquement a minuit."
                : session.Notes;

            _logger.LogWarning(
                "Session {Id} fermee automatiquement (ouverte depuis {Date})",
                session.Id,
                session.DateSession);
        }

        if (sessionsOuvertes.Count > 0)
            await context.SaveChangesAsync();
    }
}
