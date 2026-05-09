using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;

namespace Pharmacie.Tests.Support;

/// <summary>
/// Fournit un <see cref="ApplicationDbContext"/> SQLite en mémoire avec schéma créé.
/// SQLite gère les transactions — nécessaire pour <see cref="Pharmacie.Services.SaleService"/> et les réceptions.
/// </summary>
internal static class TestDbContextFactory
{
    public static (ApplicationDbContext Context, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }
}
