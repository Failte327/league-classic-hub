using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueClassic.Web.Pages;

public class PrivacyModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public PrivacyModel(IWebHostEnvironment env) => _env = env;

    public string Markdown { get; private set; } = "";

    public void OnGet()
    {
        var path = Path.Combine(_env.ContentRootPath, "Data", "content", "privacy.md");
        Markdown = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "# Privacy Policy\nComing soon.";
    }
}
