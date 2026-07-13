using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueClassic.Web.Pages;

public class RulesModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public RulesModel(IWebHostEnvironment env) => _env = env;

    public string Markdown { get; private set; } = "";

    public void OnGet()
    {
        var path = Path.Combine(_env.ContentRootPath, "Data", "content", "rules.md");
        Markdown = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "# Rules\nComing soon.";
    }
}
