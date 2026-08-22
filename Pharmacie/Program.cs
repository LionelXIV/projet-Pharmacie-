using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Pharmacie.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Services;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("PharmacieApp");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministrator", policy =>
        policy.RequireRole(AppRoles.PharmacienTitulaire, AppRoles.Administrateur));

    options.AddPolicy("ProductSearch", policy =>
        policy.RequireAssertion(context =>
            AppRoles.CanAccessCatalog(context.User)
            || AppRoles.CanAccessSales(context.User)
            || AppRoles.CanAccessPurchasing(context.User)));
});

builder.Services.Configure<RazorPagesOptions>(options =>
{
    options.Conventions.AuthorizeAreaPage("Identity", "/Account/Register", "RequireAdministrator");
});

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 4;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Cookie de session : 12h (journée de travail), sliding, MaxAge null.
// La déconnexion explicite se fait via le bouton « Terminer la session ».
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.MaxAge = null;
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
});

builder.Services.AddControllersWithViews();
builder.Services.Configure<FeatureFlags>(
    builder.Configuration.GetSection("Features"));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    // Cookie de session navigateur (séparé d'Identity) — sans MaxAge → disparaît à la fermeture
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.Name = ".Pharmacie.BrowserSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<ExcelReaderService>();
builder.Services.AddScoped<BlImportService>();
builder.Services.AddScoped<ImportValidationService>();
builder.Services.AddScoped<ImportMatchingService>();
builder.Services.AddScoped<ProductImportService>();
builder.Services.AddScoped<UserActivityReportService>();
builder.Services.AddScoped<BonService>();
builder.Services.AddScoped<AvoirService>();
builder.Services.AddScoped<CaisseService>();

var app = builder.Build();

if (args.Contains("--reset-data", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Pharmacie.ResetData");

    // Azure SQL + gros volumes : le timeout par défaut (30s) est trop court
    context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

    Console.WriteLine("=== Remise a zero des donnees metier (--reset-data) ===");
    Console.WriteLine("Conserve : AspNetUsers/Roles, Categories, Suppliers, Products (catalogue + anomalies)");
    Console.WriteLine("Supprime : ventes, caisse, bons, avoirs, patients, BL, commandes, lots, mouvements, imports, histo prix, Vendeurs");
    Console.WriteLine("Conseil : arrete temporairement l'App Service Azure pour liberer les verrous.");

    await using var transaction = await context.Database.BeginTransactionAsync();
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE ProductBatches SET SourceImportLineId = NULL WHERE SourceImportLineId IS NOT NULL");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE ImportLines SET MatchedProductId = NULL, CreatedBatchId = NULL WHERE MatchedProductId IS NOT NULL OR CreatedBatchId IS NOT NULL");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE StockMovements SET SaleId = NULL WHERE SaleId IS NOT NULL");

        // Ventes et caisses
        await context.VenteCaisses.ExecuteDeleteAsync();
        await context.DepotCaisses.ExecuteDeleteAsync();
        await context.SessionCaisses.ExecuteDeleteAsync();
        await context.SaleLines.ExecuteDeleteAsync();
        await context.Sales.ExecuteDeleteAsync();

        // Bons et avoirs
        await context.ReglementBons.ExecuteDeleteAsync();
        await context.BonLignes.ExecuteDeleteAsync();
        await context.Bons.ExecuteDeleteAsync();
        await context.AvoirLignes.ExecuteDeleteAsync();
        await context.Avoirs.ExecuteDeleteAsync();

        // Patients
        await context.PatientTreatmentReminders.ExecuteDeleteAsync();
        await context.PatientPrescriptions.ExecuteDeleteAsync();
        await context.Patients.ExecuteDeleteAsync();

        // BL et commandes fournisseur (tests)
        await context.GoodsReceiptLines.ExecuteDeleteAsync();
        await context.GoodsReceipts.ExecuteDeleteAsync();
        await context.PurchaseOrderLines.ExecuteDeleteAsync();
        await context.PurchaseOrders.ExecuteDeleteAsync();

        // Stock et mouvements — Products conserve
        await context.StockMovements.ExecuteDeleteAsync();
        await context.ProductBatches.ExecuteDeleteAsync();
        await context.Database.ExecuteSqlRawAsync("UPDATE Products SET StockQuantity = 0");

        // Imports et traces
        await context.ImportAnomalies.ExecuteDeleteAsync();
        await context.ImportLines.ExecuteDeleteAsync();
        await context.ImportBatches.ExecuteDeleteAsync();
        await context.PrixModifications.ExecuteDeleteAsync();
        await context.UserActivityReports.ExecuteDeleteAsync();

        // Vendeurs (liste encaissement)
        await context.Vendeurs.ExecuteDeleteAsync();

        foreach (var table in new[]
                 {
                     "Sales", "SaleLines", "SessionCaisses", "DepotCaisses", "VenteCaisses",
                     "Bons", "BonLignes", "ReglementBons", "Avoirs", "AvoirLignes",
                     "Patients", "PatientPrescriptions", "PatientTreatmentReminders",
                     "GoodsReceiptLines", "GoodsReceipts", "PurchaseOrderLines", "PurchaseOrders",
                     "StockMovements", "ProductBatches",
                     "ImportBatches", "ImportLines", "ImportAnomalies",
                     "PrixModifications", "UserActivityReports", "Vendeurs"
                 })
        {
            await context.Database.ExecuteSqlRawAsync($"DBCC CHECKIDENT ('{table}', RESEED, 0)");
        }

        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        logger.LogError(ex, "Echec du reset --reset-data");
        Console.Error.WriteLine("ERREUR: reset annule (rollback). " + ex.Message);
        Environment.ExitCode = 1;
        return;
    }

    var productsLeft = await context.Products.CountAsync();
    var salesLeft = await context.Sales.CountAsync();
    var usersLeft = await context.Users.CountAsync();
    var vendeursLeft = await context.Vendeurs.CountAsync();
    var bonsLeft = await context.Bons.CountAsync();
    var blLeft = await context.GoodsReceipts.CountAsync();
    var caisseLeft = await context.SessionCaisses.CountAsync();
    var lotsLeft = await context.ProductBatches.CountAsync();

    Console.WriteLine("OK: nettoyage complet termine (Products conserve, BL + Vendeurs supprimes).");
    Console.WriteLine($"OK: Utilisateurs={usersLeft}, produits={productsLeft}, vendeurs={vendeursLeft} (attendu 0).");
    Console.WriteLine($"Verification: Sales={salesLeft}, Bons={bonsLeft}, BL={blLeft}, SessionsCaisse={caisseLeft}, Lots={lotsLeft} (attendu 0).");
    logger.LogWarning(
        "RESET DATA CLI effectue. Users={Users}, Vendeurs={Vendeurs}, Products={Products}, Sales={Sales}",
        usersLeft, vendeursLeft, productsLeft, salesLeft);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Identity cookie peut survivre à la fermeture Chrome ; le cookie Session disparaît.
// Si Identity est authentifié mais SessionStart absent → nouvelle session navigateur → déconnexion.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/Identity/Account/Login")
        || path.StartsWithSegments("/Identity/Account/Logout")
        || path.StartsWithSegments("/logout-silent")
        || path.StartsWithSegments("/sw.js")
        || path.StartsWithSegments("/manifest.json")
        || path.StartsWithSegments("/icons")
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/lib")
        || path.StartsWithSegments("/favicon");

    if (!IsExempt(path)
        && context.User.Identity?.IsAuthenticated == true)
    {
        var sessionStart = context.Session.GetString("SessionStart");
        if (string.IsNullOrEmpty(sessionStart))
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Session.Clear();
            context.Response.Redirect("/Identity/Account/Login");
            return;
        }
    }

    await next();
});

app.MapStaticAssets();

app.MapControllers();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.MapPost("/logout-silent", async (HttpContext ctx, SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    ctx.Session.Clear();
    return Results.Ok();
}).RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeed.SeedRolesAsync(roleManager);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Pharmacie.Data.IdentitySeed");
    await IdentitySeed.SeedInitialAdminIfMissingAsync(
        userManager, configuration, app.Environment, seedLogger);
    await IdentitySeed.MigrateLegacyRoleAssignmentsAsync(userManager, seedLogger);

    if (app.Environment.IsDevelopment())
    {
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
        var purchase = scope.ServiceProvider.GetRequiredService<PurchaseService>();
        var saleSvc = scope.ServiceProvider.GetRequiredService<SaleService>();
        var adminEmail = configuration[IdentitySeed.AdminEmailConfigKey]?.Trim();
        var adminUser = !string.IsNullOrEmpty(adminEmail)
            ? await userManager.FindByEmailAsync(adminEmail)
            : null;
        await DemoDataSeed.SeedIfNeededAsync(db, inventory, purchase, saleSvc, adminUser?.Id);
    }
}

app.Run();
