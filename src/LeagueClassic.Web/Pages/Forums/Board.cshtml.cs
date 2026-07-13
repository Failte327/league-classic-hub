using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Forums;

public class BoardModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public BoardModel(ApplicationDbContext db) => _db = db;

    public Board Board { get; private set; } = default!;
    public List<ForumThread> Threads { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var board = await _db.Boards.AsNoTracking().FirstOrDefaultAsync(b => b.Slug == slug);
        if (board is null) return NotFound();
        Board = board;

        Threads = await _db.Threads
            .Where(t => t.BoardId == board.Id)
            .Include(t => t.Author)
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.LastPostAt)
            .AsNoTracking()
            .ToListAsync();

        return Page();
    }
}
