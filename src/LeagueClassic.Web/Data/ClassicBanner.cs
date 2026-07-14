namespace LeagueClassic.Web.Data;

// Shared backdrop art for the Forums / Guides / My Guides list-page banners —
// a dim, desaturated classic map behind an otherwise text-heavy header. Kept
// to just the map variants; the golem/Nexus art didn't read well at this size.
public static class ClassicBanner
{
    private static readonly string[] Art =
    {
        "map-summoners-rift.jpg",
        "map-parchment.jpg",
    };

    public static string RandomFile() => Art[Random.Shared.Next(Art.Length)];
}
