using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Reference;

public class ItemsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ItemsModel(ApplicationDbContext db) => _db = db;

    public ILookup<string, Item> ByCategory { get; private set; } = Enumerable.Empty<Item>().ToLookup(i => "");

    public async Task OnGetAsync()
    {
        var items = await _db.Items.OrderBy(i => i.Category).ThenBy(i => i.Name).AsNoTracking().ToListAsync();
        ByCategory = items.ToLookup(i => i.Category);
    }
}
