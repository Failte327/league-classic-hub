using System.ComponentModel.DataAnnotations;
using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ContentModerationService _moderation;

    public RegisterModel(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, ContentModerationService moderation)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _moderation = moderation;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(24, MinimumLength = 3, ErrorMessage = "Your username must be 3–24 characters.")]
        [RegularExpression(@"^[A-Za-z0-9 _.\-]+$", ErrorMessage = "Letters, numbers, spaces, and _ . - only.")]
        [Display(Name = "Username")]
        public string DisplayName { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        var name = Input.DisplayName.Trim();

        if (_moderation.ContainsBlockedTerm(name))
            ModelState.AddModelError("Input.DisplayName", "That username isn't allowed. Please pick another.");
        else if (await _userManager.Users.AnyAsync(u => u.DisplayName!.ToLower() == name.ToLower()))
            ModelState.AddModelError("Input.DisplayName", "That username is taken. Please pick another.");

        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Email,   // email stays the login
            Email = Input.Email,
            DisplayName = name,
        };
        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(returnUrl ?? Url.Content("~/"));
    }
}
