namespace LeagueClassic.Web.Data;

public static class UserDisplay
{
    // The public name to show for a user. Never the email. Falls back to a
    // generic label for accounts without a display name (or staff/null authors).
    public static string Display(this ApplicationUser? user, string fallback = "Summoner")
        => string.IsNullOrWhiteSpace(user?.DisplayName) ? fallback : user!.DisplayName!;
}
