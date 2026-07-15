using LeagueClassic.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Services;

// Shared upvote/downvote toggle logic for guides and forum posts, keeping
// each target's denormalized Score column in sync with its Vote rows.
public class VotingService
{
    private readonly ApplicationDbContext _db;

    public VotingService(ApplicationDbContext db) => _db = db;

    // Casts (or retracts/flips) a vote. value must be +1 or -1.
    public async Task CastAsync(VoteTargetType type, int targetId, string voterId, short value)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v =>
            v.TargetType == type && v.TargetId == targetId && v.VoterId == voterId);

        int delta;
        if (vote is null)
        {
            _db.Votes.Add(new Vote { TargetType = type, TargetId = targetId, VoterId = voterId, Value = value });
            delta = value;
        }
        else if (vote.Value == value)
        {
            _db.Votes.Remove(vote);
            delta = -value;
        }
        else
        {
            delta = value - vote.Value;
            vote.Value = value;
        }

        if (type == VoteTargetType.Guide)
        {
            var guide = await _db.Guides.FirstOrDefaultAsync(g => g.Id == targetId);
            if (guide is not null) guide.Score += delta;
        }
        else
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == targetId);
            if (post is not null) post.Score += delta;
        }

        await _db.SaveChangesAsync();
    }

    // The signed-in user's own vote (if any) on each of the given targets.
    public async Task<Dictionary<int, short>> MyVotesAsync(VoteTargetType type, IEnumerable<int> targetIds, string? voterId)
    {
        if (voterId is null) return new();
        var ids = targetIds.ToList();
        return await _db.Votes
            .Where(v => v.TargetType == type && v.VoterId == voterId && ids.Contains(v.TargetId))
            .ToDictionaryAsync(v => v.TargetId, v => v.Value);
    }
}
