namespace LeagueClassic.Web.Data;

public enum VoteTargetType { Guide = 0, Post = 1 }

// A user's up/down vote on a guide or forum post. One vote per user per
// target: casting the same direction again retracts it, the opposite
// direction flips it. Guide.Score / Post.Score are the denormalized tally,
// kept in sync by VotingService.
public class Vote
{
    public int Id { get; set; }

    public required string VoterId { get; set; }
    public ApplicationUser? Voter { get; set; }

    public VoteTargetType TargetType { get; set; }
    public int TargetId { get; set; }

    public short Value { get; set; } // +1 or -1

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
