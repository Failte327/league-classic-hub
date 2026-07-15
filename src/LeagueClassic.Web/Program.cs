using System.Threading.RateLimiting;
using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Identity;
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
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Reject blocked usernames during registration/profile updates.
builder.Services.AddScoped<IUserValidator<ApplicationUser>, UsernameModerationValidator>();

builder.Services.AddRazorPages();

// Persist Data Protection keys to disk (mounted to a volume in production) so
// login sessions and antiforgery tokens survive container restarts/redeploys
// instead of silently invalidating on every deploy.
var keysDir = builder.Configuration["DataProtectionKeysPath"];
if (!string.IsNullOrEmpty(keysDir))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
        .SetApplicationName("LeagueClassic");
}

// --- Output caching: the biggest lever for surviving a traffic spike. ---
// Anonymous readers get cached rendered HTML so the origin barely gets touched.
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(30)));
});

builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddSingleton<ContentModerationService>();
builder.Services.AddScoped<GuideEditorService>();
builder.Services.AddScoped<VotingService>();

// Rate limiting to blunt spam-flooding of the posting endpoints.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("post", context =>
    {
        var key = context.User.Identity?.IsAuthenticated == true
            ? context.User.Identity!.Name!
            : context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(30),
        });
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseOutputCache();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Live markdown preview for the guide editor — renders through the same
// sanitizer as the real page, so the preview matches what gets published.
app.MapPost("/guides/preview", async (HttpContext ctx, MarkdownRenderer md) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var body = form["Input.BodyMarkdown"].ToString();
    return Results.Content(md.ToHtml(body).Value ?? string.Empty, "text/html");
}).RequireAuthorization().DisableAntiforgery();

// Apply migrations + seed starter content on startup.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.Run();
