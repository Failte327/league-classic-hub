using System.ComponentModel.DataAnnotations;
using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ContentModerationService _moderation;

    public ProfileModel(UserManager<ApplicationUser> users, ContentModerationService moderation)
    {
        _users = users;
        _moderation = moderation;
    }

    public string Email { get; private set; } = "";

    [BindProperty]
    [Required]
    [StringLength(24, MinimumLength = 3, ErrorMessage = "Your username must be 3–24 characters.")]
    [RegularExpression(@"^[A-Za-z0-9 _.\-]+$", ErrorMessage = "Letters, numbers, spaces, and _ . - only.")]
    [Display(Name = "Username")]
    public string DisplayName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();
        Email = user.Email ?? "";
        DisplayName = user.DisplayName ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();
        Email = user.Email ?? "";

        var name = (DisplayName ?? "").Trim();
        if (_moderation.ContainsBlockedTerm(name))
            ModelState.AddModelError(nameof(DisplayName), "That username isn't allowed. Please pick another.");
        else if (await _users.Users.AnyAsync(u => u.DisplayName!.ToLower() == name.ToLower() && u.Id != user.Id))
            ModelState.AddModelError(nameof(DisplayName), "That username is taken. Please pick another.");

        if (!ModelState.IsValid) return Page();

        user.DisplayName = name;
        await _users.UpdateAsync(user);
        TempData["Message"] = "Your username has been updated.";
        return RedirectToPage();
    }
}
