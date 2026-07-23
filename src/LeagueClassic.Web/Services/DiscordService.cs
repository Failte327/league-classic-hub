using System.Net.Http.Json;
using System.Text.Json;

namespace LeagueClassic.Web.Services;

// Talks to Discord on two fronts:
//  - member/online counts for the site badge, via the public (unauthenticated)
//    invite-lookup endpoint — no bot token needed;
//  - a webhook post when a guide gets published, if a webhook URL is configured.
// Both are best-effort: failures are logged and swallowed rather than surfaced,
// since Discord being unreachable should never break guide publishing or page loads.
public class DiscordService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static (int Members, int Online, DateTimeOffset FetchedAt)? _cache;

    private readonly HttpClient _http;
    private readonly ILogger<DiscordService> _logger;
    private readonly string? _inviteCode;
    private readonly string? _webhookUrl;

    public DiscordService(HttpClient http, IConfiguration config, ILogger<DiscordService> logger)
    {
        _http = http;
        _logger = logger;
        _inviteCode = config["Discord:InviteCode"];
        _webhookUrl = config["Discord:WebhookUrl"];
    }

    public async Task<(int Members, int Online)?> GetMemberCountAsync()
    {
        if (string.IsNullOrEmpty(_inviteCode)) return null;
        if (_cache is { } fresh && DateTimeOffset.UtcNow - fresh.FetchedAt < CacheTtl)
            return (fresh.Members, fresh.Online);

        await CacheLock.WaitAsync();
        try
        {
            if (_cache is { } fresh2 && DateTimeOffset.UtcNow - fresh2.FetchedAt < CacheTtl)
                return (fresh2.Members, fresh2.Online);

            using var resp = await _http.GetAsync($"https://discord.com/api/v10/invites/{_inviteCode}?with_counts=true");
            if (!resp.IsSuccessStatusCode)
                return _cache is { } stale ? (stale.Members, stale.Online) : null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync());
            var members = doc.RootElement.GetProperty("approximate_member_count").GetInt32();
            var online = doc.RootElement.GetProperty("approximate_presence_count").GetInt32();
            _cache = (members, online, DateTimeOffset.UtcNow);
            return (members, online);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Discord member count");
            return _cache is { } stale ? (stale.Members, stale.Online) : null;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    public async Task NotifyGuidePublishedAsync(string title, string championName, string authorName, string url)
    {
        if (string.IsNullOrEmpty(_webhookUrl)) return;
        try
        {
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = $"New guide: {title}",
                        description = $"{championName} guide by **{authorName}**",
                        url,
                        color = 0xC89B3C,
                    },
                },
            };
            using var resp = await _http.PostAsJsonAsync(_webhookUrl, payload);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Discord webhook post failed with {Status}", resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post guide-published notification to Discord");
        }
    }
}
