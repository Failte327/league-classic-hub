using Microsoft.AspNetCore.Identity;

namespace LeagueClassic.Web.Data;

// Extends the built-in Identity user. Add profile fields here (avatar, signature,
// title, etc.) as the "classic" profile features land post-launch.
public class ApplicationUser : IdentityUser
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Denormalized so profile/post displays never COUNT() across the posts table.
    public int PostCount { get; set; }
}
