namespace LeagueClassic.Web.Data;

public enum TournamentFormat { SingleElimination, DoubleElimination, RoundRobinToSingleElim, RoundRobinToDoubleElim }
public enum TournamentStatus { RegistrationOpen, InProgress, Completed }
public enum SeedingMode { Random, Chosen }
public enum PrizeType { BraggingRights, Cash }
public enum MatchStage { GroupStage, Bracket }
public enum BracketSide { Winners, Losers, GrandFinal }

// Pending = at least one team slot still awaiting a feeder result. Ready = both slots known,
// awaiting the organizer to record a winner. Completed = winner recorded.
public enum MatchStatus { Pending, Ready, Completed }

public class Tournament
{
    public int Id { get; set; }

    public string? OrganizerId { get; set; }
    public ApplicationUser? Organizer { get; set; }

    public required string Name { get; set; }
    public required string Slug { get; set; }

    // Organizer-authored freeform info — rules, start-time notes, format clarifications,
    // whatever's relevant — rendered as markdown. Content-moderated same as every other
    // free-text field on the site; null/blank = nothing posted yet.
    public string? DetailsMarkdown { get; set; }

    public TournamentFormat Format { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.RegistrationOpen;

    public int MaxTeams { get; set; }             // 2..64
    public int RegisteredTeamCount { get; set; }   // denormalized, ApplicationUser.PostCount-style

    // Meaningful only for SingleElimination/DoubleElimination — round-robin formats seed the
    // elimination stage from group placement instead (see TournamentBracketService).
    public SeedingMode SeedingMode { get; set; }

    // Round-robin formats only.
    public int? TeamsPerGroup { get; set; }
    public int? AdvancePerGroup { get; set; }

    public PrizeType PrizeType { get; set; } = PrizeType.BraggingRights;
    public decimal? PrizeAmount { get; set; }
    public string? PrizeCurrency { get; set; }     // free text, e.g. "USD"

    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<TournamentTeam> Teams { get; set; } = new();
    public List<TournamentGroup> Groups { get; set; } = new();
    public List<TournamentMatch> Matches { get; set; } = new();
}

public class TournamentGroup
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public int Index { get; set; }         // 0-based; display name = "Group " + (char)('A' + Index)
    public bool IsConcluded { get; set; }

    public List<TournamentTeam> Teams { get; set; } = new();
}

public class TournamentTeam
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public string? CaptainId { get; set; }     // the registering user
    public ApplicationUser? Captain { get; set; }

    public required string Name { get; set; }  // content-moderated at registration

    public int? Seed { get; set; }             // elimination formats, assigned at Start
    public int? GroupId { get; set; }          // round-robin formats
    public TournamentGroup? Group { get; set; }
    public int? GroupRank { get; set; }        // finalized within-group placement (1-based)

    public int Wins { get; set; }
    public int Losses { get; set; }

    public bool IsEliminated { get; set; }
    public int? FinalRank { get; set; }        // 1/2/3, set once Tournament.Status == Completed

    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    public List<TournamentPlayer> Players { get; set; } = new();
}

public class TournamentPlayer
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public TournamentTeam? Team { get; set; }

    public required string Name { get; set; }  // content-moderated at registration; unverified
    public int Sort { get; set; }
}

// Bracket represented as a match-node graph: NextMatchId/LoserNextMatchId self-reference the
// team's winner/loser forward into the next match that depends on this one. This generalizes
// to double-elimination's losers-bracket "drop" pattern, which implicit (Round, Slot) math
// handles poorly. Round-robin group matches don't use the Next*/BracketSide fields at all.
public class TournamentMatch
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public MatchStage Stage { get; set; }

    // --- Group stage (Stage == GroupStage) ---
    public int? GroupId { get; set; }
    public TournamentGroup? Group { get; set; }
    public int? GroupRound { get; set; }       // 1-based round-robin round within the group, display order only

    // --- Bracket (Stage == Bracket) ---
    public BracketSide? BracketSide { get; set; }
    public int? Round { get; set; }            // 1-based within (Stage, BracketSide)
    public int? SlotIndex { get; set; }        // 0-based position within (Stage, BracketSide, Round)

    public int? NextMatchId { get; set; }
    public TournamentMatch? NextMatch { get; set; }
    public int? NextMatchSlot { get; set; }    // 0 or 1 — which of NextMatch's two team slots the winner fills

    public int? LoserNextMatchId { get; set; } // double-elimination only: where the loser drops to
    public TournamentMatch? LoserNextMatch { get; set; }
    public int? LoserNextMatchSlot { get; set; }

    // --- Common ---
    public int? TeamAId { get; set; }
    public TournamentTeam? TeamA { get; set; }
    public int? TeamBId { get; set; }
    public TournamentTeam? TeamB { get; set; }

    public bool TeamAIsBye { get; set; }
    public bool TeamBIsBye { get; set; }

    public int? WinnerTeamId { get; set; }
    public TournamentTeam? WinnerTeam { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Pending;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RecordedById { get; set; }  // audit: who clicked the winner
}
