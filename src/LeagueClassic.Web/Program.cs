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

// Password-reset/confirmation emails, sent via SES SMTP in production. Falls
// back to a logging no-op locally so `dotnet run` doesn't need real SES
// credentials — see SesEmailSender.cs.
if (!string.IsNullOrEmpty(builder.Configuration["Email:SmtpUsername"]))
    builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SesEmailSender>();
else
    builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, NullEmailSender>();

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
builder.Services.AddSingleton<TournamentBracketService>();
builder.Services.AddScoped<TournamentService>();
builder.Services.AddHttpClient<DiscordService>();

// Rate limiting to blunt spam-flooding of the posting endpoints. The "post"
// policy is applied at the PageModel level (Razor Pages doesn't honor
// [EnableRateLimiting] on individual handler methods), which would otherwise
// also throttle plain page views (OnGet) sharing that page with a POST
// handler — e.g. reloading a guide/thread a few times while checking on it
// would trip the limiter and render a blank 429. Only counting POST/PUT/etc.
// requests toward the limit keeps GETs unrestricted while still limiting the
// actual write actions (comments, votes, replies).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("post", context =>
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            return RateLimitPartition.GetNoLimiter("unlimited");

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

// Sitemap for search engines — static top-level pages plus every published
// guide, champion, and tournament. Regenerated per-request; traffic here is
// low enough that querying fresh each time isn't worth caching.
app.MapGet("/sitemap.xml", async (ApplicationDbContext db) =>
{
    const string siteUrl = "https://leagueclassicarchive.net";
    var urls = new List<string>
    {
        siteUrl,
        $"{siteUrl}/Guides",
        $"{siteUrl}/Champions",
        $"{siteUrl}/Forums",
        $"{siteUrl}/Resources/Items",
        $"{siteUrl}/Tournaments",
        $"{siteUrl}/Rules",
    };

    urls.AddRange(await db.Champions.Select(c => $"{siteUrl}/Champions/Details/{c.Slug}").ToListAsync());
    urls.AddRange(await db.Guides
        .Where(g => g.Status == GuideStatus.Published)
        .Select(g => $"{siteUrl}/Guides/Details/{g.Slug}")
        .ToListAsync());
    urls.AddRange(await db.Tournaments.Select(t => $"{siteUrl}/Tournaments/Details/{t.Slug}").ToListAsync());

    var body = string.Concat(urls.Select(u => $"<url><loc>{u}</loc></url>"));
    var xml = $"""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">{body}</urlset>""";
    return Results.Text(xml, "application/xml");
});

// Live markdown preview for the guide editor — renders through the same
// sanitizer as the real page, so the preview matches what gets published.
app.MapPost("/guides/preview", async (HttpContext ctx, MarkdownRenderer md) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var body = form["Input.BodyMarkdown"].ToString();
    return Results.Content(md.ToHtml(body).Value ?? string.Empty, "text/html");
}).RequireAuthorization().DisableAntiforgery();

// Live markdown preview for the tournament details/rules editor.
app.MapPost("/tournaments/details-preview", async (HttpContext ctx, MarkdownRenderer md) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var body = form["Input.DetailsMarkdown"].ToString();
    return Results.Content(md.ToHtml(body).Value ?? string.Empty, "text/html");
}).RequireAuthorization().DisableAntiforgery();

// Apply migrations + seed starter content on startup.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.Run();
