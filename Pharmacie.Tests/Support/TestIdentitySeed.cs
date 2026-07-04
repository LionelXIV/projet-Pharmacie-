using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;

namespace Pharmacie.Tests.Support;

/// <summary>Utilisateur Identity minimal pour les tests nécessitant des FK vers AspNetUsers.</summary>
internal static class TestIdentitySeed
{
    public const string DefaultUserId = "test-user-1";

    public static async Task<string> EnsureUserAsync(
        ApplicationDbContext db,
        string userId = DefaultUserId)
    {
        if (await db.Users.AnyAsync(u => u.Id == userId))
            return userId;

        db.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@test.local".ToUpperInvariant()
        });
        await db.SaveChangesAsync();
        return userId;
    }
}
