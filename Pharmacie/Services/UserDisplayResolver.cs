using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;

namespace Pharmacie.Services;

/// <summary>Libellés lisibles pour les utilisateurs Identity (traçabilité légère, sans table d’audit dédiée).</summary>
public static class UserDisplayResolver
{
    public static string Format(string? email, string? userName)
    {
        var e = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        var u = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
        if (e != null && u != null && !string.Equals(e, u, StringComparison.OrdinalIgnoreCase))
            return $"{u} — {e}";
        return e ?? u ?? "—";
    }

    public static async Task<Dictionary<string, string>> LoadLabelsByIdAsync(
        ApplicationDbContext db,
        IEnumerable<string?> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, string>();

        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => Format(u.Email, u.UserName),
                cancellationToken);
    }

    /// <summary>Affiche le libellé connu, ou une mention courte si l’utilisateur n’existe plus en base.</summary>
    public static string Resolve(Dictionary<string, string> labelsById, string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return "—";

        if (labelsById.TryGetValue(userId, out var label))
            return label;

        var prefix = userId.Length <= 8 ? userId : userId[..8];
        return $"Compte introuvable ({prefix}…)";
    }
}
