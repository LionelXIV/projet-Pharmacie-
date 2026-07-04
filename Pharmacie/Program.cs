using Microsoft.AspNetCore.Identity;
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

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
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
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<ExcelReaderService>();
builder.Services.AddScoped<ImportValidationService>();
builder.Services.AddScoped<ImportMatchingService>();
builder.Services.AddScoped<ProductImportService>();

var app = builder.Build();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (app.Environment.IsDevelopment())
        await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeed.SeedRolesAsync(roleManager);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
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
