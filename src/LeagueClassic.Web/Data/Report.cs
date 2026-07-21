namespace LeagueClassic.Web.Data;

public enum ReportTargetType { Post = 0, Guide = 1, Team = 2 }

public enum ReportReason { Spam = 0, Harassment = 1, HateSpeech = 2, Nsfw = 3, OffTopic = 4, Other = 5 }

public enum ReportStatus { Open = 0, Resolved = 1, Dismissed = 2 }

// A user report against a forum post or a guide, reviewed by moderators.
public class Report
{
    public int Id { get; set; }

    public string? ReporterId { get; set; }
    public ApplicationUser? Reporter { get; set; }

    public ReportTargetType TargetType { get; set; }
    public int TargetId { get; set; }

    public ReportReason Reason { get; set; }
    public string? Details { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ResolvedById { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
