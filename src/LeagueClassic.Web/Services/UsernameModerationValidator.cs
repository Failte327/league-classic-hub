using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace LeagueClassic.Web.Services;

// Runs during Identity registration (and profile updates): rejects usernames
// containing blocked terms. Registered alongside Identity's default validators.
public class UsernameModerationValidator : IUserValidator<ApplicationUser>
{
    private readonly ContentModerationService _moderation;

    public UsernameModerationValidator(ContentModerationService moderation) => _moderation = moderation;

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        if (_moderation.ContainsBlockedTerm(user.UserName))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "BlockedUserName",
                Description = "That username isn't allowed. Please choose another.",
            }));
        }
        return Task.FromResult(IdentityResult.Success);
    }
}
