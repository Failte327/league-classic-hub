using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// --- Database (PostgreSQL via EF Core) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. Set it in appsettings.Development.json " +
        "or via the env var ConnectionStrings__DefaultConnection.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(connectionString)
        // EF 10's runtime Migrate() flags a false-positive model-vs-snapshot diff
        // for this Identity model even when the design-time drift check
        // (`dotnet ef migrations has-pending-model-changes`) reports clean.
        // We generate migrations explicitly, so that CLI check — run in CI — is
        // the real guard; ignore the over-eager startup warning here.
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// --- Identity (login/register handled by the built-in Identity UI) ---
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        // Relaxed for dev; tighten (RequireConfirmedAccount=true) before launch.
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();

// --- Output caching: the biggest lever for surviving a traffic spike. ---
// Anonymous readers get cached rendered HTML so the origin barely gets touched.
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(30)));
});

builder.Services.AddSingleton<MarkdownRenderer>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Apply migrations + seed starter content on startup.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.Run();
