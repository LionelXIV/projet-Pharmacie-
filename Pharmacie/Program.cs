using Microsoft.AspNetCore.Identity;
using Pharmacie.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministrator", policy =>
        policy.RequireRole(AppRoles.Administrateur));

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

// Cookie de session strict : MaxAge null + isPersistent:false à la connexion.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = false;
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.MaxAge = null;
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
});

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<ExcelReaderService>();
builder.Services.AddScoped<ImportValidationService>();
builder.Services.AddScoped<ImportMatchingService>();
builder.Services.AddScoped<ProductImportService>();
builder.Services.AddScoped<UserActivityReportService>();

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
    Console.WriteLine("Conserve : AspNetUsers/Roles, Categories, Suppliers, Vendeurs");
    Console.WriteLine("Supprime : imports, patients, ventes, achats, stock, produits, rapports activite");
    Console.WriteLine("Conseil : arrete temporairement l'App Service Azure pour liberer les verrous.");

    await using var transaction = await context.Database.BeginTransactionAsync();
    try
    {
        // Couper les FK croisees avant DELETE (evite SET NULL / Restrict lents sous verrous)
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE ProductBatches SET SourceImportLineId = NULL WHERE SourceImportLineId IS NOT NULL");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE ImportLines SET MatchedProductId = NULL, CreatedBatchId = NULL WHERE MatchedProductId IS NOT NULL OR CreatedBatchId IS NOT NULL");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE StockMovements SET SaleId = NULL WHERE SaleId IS NOT NULL");

        // Ordre FK — ne pas toucher Identity / Categories / Suppliers / Vendeurs
        await context.ImportAnomalies.ExecuteDeleteAsync();
        await context.ImportLines.ExecuteDeleteAsync();
        await context.ImportBatches.ExecuteDeleteAsync();
        await context.UserActivityReports.ExecuteDeleteAsync();
        await context.PatientTreatmentReminders.ExecuteDeleteAsync();
        await context.PatientPrescriptions.ExecuteDeleteAsync();
        await context.Patients.ExecuteDeleteAsync();
        await context.SaleLines.ExecuteDeleteAsync();
        await context.Sales.ExecuteDeleteAsync();
        await context.GoodsReceiptLines.ExecuteDeleteAsync();
        await context.GoodsReceipts.ExecuteDeleteAsync();
        await context.PurchaseOrderLines.ExecuteDeleteAsync();
        await context.PurchaseOrders.ExecuteDeleteAsync();
        await context.StockMovements.ExecuteDeleteAsync();
        await context.ProductBatches.ExecuteDeleteAsync();
        await context.Products.ExecuteDeleteAsync();

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

    Console.WriteLine("OK: Donnees metier supprimees avec succes.");
    Console.WriteLine($"OK: Utilisateurs conserves ({usersLeft}), vendeurs conserves ({vendeursLeft}).");
    Console.WriteLine($"Verification: Products={productsLeft}, Sales={salesLeft} (attendu 0).");
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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.MapPost("/logout-silent", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
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
