using LeagueClassic.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Services;

// EF-aware orchestration on top of the pure TournamentBracketService: registration, starting
// a tournament (materializing a generated bracket/group-schedule into real rows), recording
// match results and propagating them through the bracket, and group-stage standings.
public class TournamentService
{
    private readonly ApplicationDbContext _db;
    private readonly TournamentBracketService _bracket;

    public TournamentService(ApplicationDbContext db, TournamentBracketService bracket)
    {
        _db = db;
        _bracket = bracket;
    }

    public async Task<TournamentTeam> RegisterTeamAsync(
        int tournamentId, string captainUserId, string teamName, IReadOnlyList<string> playerNames)
    {
        var t = await _db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (t.Status != TournamentStatus.RegistrationOpen)
            throw new InvalidOperationException("Registration is closed for this tournament.");
        if (t.RegisteredTeamCount >= t.MaxTeams)
            throw new InvalidOperationException("This tournament is full.");

        var team = new TournamentTeam { TournamentId = tournamentId, CaptainId = captainUserId, Name = teamName };
        for (var i = 0; i < playerNames.Count; i++)
            team.Players.Add(new TournamentPlayer { Name = playerNames[i], Sort = i });

        _db.TournamentTeams.Add(team);
        t.RegisteredTeamCount++;
        await _db.SaveChangesAsync();
        return team;
    }

    public async Task AssignSeedsAsync(int tournamentId, IReadOnlyDictionary<int, int> teamIdToSeed)
    {
        var teams = await _db.TournamentTeams.Where(t => t.TournamentId == tournamentId).ToListAsync();
        foreach (var team in teams)
            team.Seed = teamIdToSeed.TryGetValue(team.Id, out var s) ? s : null;
        await _db.SaveChangesAsync();
    }

    public async Task StartTournamentAsync(int tournamentId)
    {
        var t = await _db.Tournaments.Include(x => x.Teams).FirstOrDefaultAsync(x => x.Id == tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (t.Status != TournamentStatus.RegistrationOpen)
            throw new InvalidOperationException("This tournament has already started.");
        if (t.Teams.Count < 2)
            throw new InvalidOperationException("Need at least 2 registered teams to start.");

        if (t.Format is TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination)
        {
            List<TournamentTeam> ordered;
            if (t.SeedingMode == SeedingMode.Random)
            {
                ordered = t.Teams.OrderBy(_ => Guid.NewGuid()).ToList();
                for (var i = 0; i < ordered.Count; i++) ordered[i].Seed = i + 1;
            }
            else
            {
                if (t.Teams.Any(x => x.Seed is null) || t.Teams.Select(x => x.Seed).Distinct().Count() != t.Teams.Count)
                    throw new InvalidOperationException("Every team needs a unique seed assigned before starting.");
                ordered = t.Teams.OrderBy(x => x.Seed).ToList();
            }
            await _db.SaveChangesAsync();
            await MaterializeBracketAsync(t, ordered.Select(x => x.Id).ToList());
        }
        else
        {
            await StartRoundRobinAsync(t);
        }

        t.Status = TournamentStatus.InProgress;
        t.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task StartRoundRobinAsync(Tournament t)
    {
        if (t.TeamsPerGroup is null || t.AdvancePerGroup is null)
            throw new InvalidOperationException("Round-robin tournaments need group size configured.");

        var (groupCount, sizes) = _bracket.PlanGroups(t.Teams.Count, t.TeamsPerGroup.Value);
        if (t.AdvancePerGroup.Value > sizes.Min())
            throw new InvalidOperationException("Advance-per-group can't exceed the smallest group's actual size.");

        var shuffled = t.Teams.OrderBy(_ => Guid.NewGuid()).ToList();
        var groups = new List<TournamentGroup>();
        for (var g = 0; g < groupCount; g++)
        {
            var group = new TournamentGroup { TournamentId = t.Id, Index = g };
            _db.TournamentGroups.Add(group);
            groups.Add(group);
        }
        await _db.SaveChangesAsync(); // populate group ids

        var groupMembers = groups.Select(_ => new List<TournamentTeam>()).ToList();
        var idx = 0;
        for (var g = 0; g < groupCount; g++)
            for (var s = 0; s < sizes[g]; s++)
            {
                var team = shuffled[idx++];
                team.GroupId = groups[g].Id;
                groupMembers[g].Add(team);
            }
        await _db.SaveChangesAsync();

        for (var g = 0; g < groupCount; g++)
        {
            var members = groupMembers[g];
            foreach (var m in _bracket.BuildRoundRobinSchedule(members.Count))
            {
                _db.TournamentMatches.Add(new TournamentMatch
                {
                    TournamentId = t.Id,
                    Stage = MatchStage.GroupStage,
                    GroupId = groups[g].Id,
                    GroupRound = m.GroupRound,
                    TeamAId = members[m.TeamIndexA].Id,
                    TeamBId = members[m.TeamIndexB].Id,
                    Status = MatchStatus.Ready,
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<TournamentTeam>> ComputeGroupStandingsAsync(int groupId)
    {
        var teams = await _db.TournamentTeams.Where(t => t.GroupId == groupId).ToListAsync();
        var completed = await _db.TournamentMatches
            .Where(m => m.GroupId == groupId && m.Status == MatchStatus.Completed)
            .ToListAsync();

        var beat = new HashSet<(int winner, int loser)>();
        foreach (var m in completed)
        {
            if (m.WinnerTeamId is null) continue;
            var loserId = m.TeamAId == m.WinnerTeamId ? m.TeamBId : m.TeamAId;
            if (loserId.HasValue) beat.Add((m.WinnerTeamId.Value, loserId.Value));
        }

        var ordered = teams.ToList();
        ordered.Sort((x, y) =>
        {
            var byWins = y.Wins.CompareTo(x.Wins);
            if (byWins != 0) return byWins;
            if (beat.Contains((x.Id, y.Id))) return -1;
            if (beat.Contains((y.Id, x.Id))) return 1;
            return x.Id.CompareTo(y.Id); // stable fallback; organizer resolves real 3+-way ties manually
        });
        return ordered;
    }

    public async Task ConfirmGroupStandingsAsync(int groupId, IReadOnlyList<int> orderedTeamIds)
    {
        var group = await _db.TournamentGroups.FindAsync(groupId)
            ?? throw new InvalidOperationException("Group not found.");
        for (var i = 0; i < orderedTeamIds.Count; i++)
        {
            var team = await _db.TournamentTeams.FindAsync(orderedTeamIds[i]);
            if (team != null && team.GroupId == groupId) team.GroupRank = i + 1;
        }
        group.IsConcluded = true;
        await _db.SaveChangesAsync();
    }

    public async Task GenerateBracketFromGroupsAsync(int tournamentId)
    {
        var t = await _db.Tournaments
            .Include(x => x.Groups).ThenInclude(g => g.Teams)
            .FirstOrDefaultAsync(x => x.Id == tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (t.Groups.Count == 0 || t.Groups.Any(g => !g.IsConcluded))
            throw new InvalidOperationException("All groups must have confirmed standings first.");
        if (await _db.TournamentMatches.AnyAsync(m => m.TournamentId == tournamentId && m.Stage == MatchStage.Bracket))
            throw new InvalidOperationException("The playoff bracket has already been generated.");

        var advancers = t.Groups
            .OrderBy(g => g.Index)
            .SelectMany(g => g.Teams
                .Where(tm => tm.GroupRank.HasValue && tm.GroupRank <= t.AdvancePerGroup)
                .Select(tm => (group: g.Index, rank: tm.GroupRank!.Value, team: tm)))
            .OrderBy(x => x.rank).ThenBy(x => x.group)
            .Select(x => x.team)
            .ToList();

        for (var i = 0; i < advancers.Count; i++) advancers[i].Seed = i + 1;
        await _db.SaveChangesAsync();

        await MaterializeBracketAsync(t, advancers.Select(a => a.Id).ToList());
        await _db.SaveChangesAsync();
    }

    public async Task RecordMatchResultAsync(int matchId, int winnerTeamId, string recordedByUserId)
    {
        var match = await _db.TournamentMatches.FirstOrDefaultAsync(m => m.Id == matchId)
            ?? throw new InvalidOperationException("Match not found.");
        if (match.Status == MatchStatus.Completed)
            throw new InvalidOperationException("This match's result was already recorded.");
        if (match.TeamAId != winnerTeamId && match.TeamBId != winnerTeamId)
            throw new InvalidOperationException("The winner must be one of the two teams in this match.");

        var loserTeamId = match.TeamAId == winnerTeamId ? match.TeamBId : match.TeamAId;

        match.WinnerTeamId = winnerTeamId;
        match.Status = MatchStatus.Completed;
        match.CompletedAt = DateTimeOffset.UtcNow;
        match.RecordedById = recordedByUserId;

        if (match.Stage == MatchStage.GroupStage)
        {
            var winner = await _db.TournamentTeams.FindAsync(winnerTeamId);
            if (winner != null) winner.Wins++;
            if (loserTeamId.HasValue)
            {
                var loser = await _db.TournamentTeams.FindAsync(loserTeamId.Value);
                if (loser != null) loser.Losses++;
            }
            await _db.SaveChangesAsync();
            return;
        }

        if (match.NextMatchId.HasValue)
            await FeedMatchSlotAsync(match.NextMatchId.Value, match.NextMatchSlot!.Value, winnerTeamId);
        if (match.LoserNextMatchId.HasValue && loserTeamId.HasValue)
            await FeedMatchSlotAsync(match.LoserNextMatchId.Value, match.LoserNextMatchSlot!.Value, loserTeamId.Value);
        await _db.SaveChangesAsync();

        if (match.NextMatchId is null)
        {
            if (match.BracketSide == BracketSide.GrandFinal && match.Round == 1 && winnerTeamId == match.TeamBId)
            {
                // Bracket reset: the losers-bracket side beat the winners-bracket side in game
                // one. The WB side needed a first loss to be eliminated, so they get a rematch.
                _db.TournamentMatches.Add(new TournamentMatch
                {
                    TournamentId = match.TournamentId,
                    Stage = MatchStage.Bracket,
                    BracketSide = BracketSide.GrandFinal,
                    Round = 2,
                    SlotIndex = 0,
                    TeamAId = match.TeamAId,
                    TeamBId = match.TeamBId,
                    Status = MatchStatus.Ready,
                });
                await _db.SaveChangesAsync();
            }
            else
            {
                await FinalizeAsync(match, winnerTeamId, loserTeamId);
            }
        }
    }

    private async Task FinalizeAsync(TournamentMatch finalMatch, int winnerTeamId, int? loserTeamId)
    {
        var tournament = await _db.Tournaments.FindAsync(finalMatch.TournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        var winner = await _db.TournamentTeams.FindAsync(winnerTeamId);
        if (winner != null) winner.FinalRank = 1;
        if (loserTeamId.HasValue)
        {
            var loser = await _db.TournamentTeams.FindAsync(loserTeamId.Value);
            if (loser != null) loser.FinalRank = 2;
        }

        if (tournament.Format is TournamentFormat.DoubleElimination or TournamentFormat.RoundRobinToDoubleElim)
        {
            // 3rd place: the losers-bracket final's loser (unambiguous). It feeds into the
            // original (round 1) grand final's slot 1.
            var gf1 = await _db.TournamentMatches.FirstOrDefaultAsync(m =>
                m.TournamentId == tournament.Id && m.BracketSide == BracketSide.GrandFinal && m.Round == 1);
            var lbFinal = gf1 is null ? null : await _db.TournamentMatches.FirstOrDefaultAsync(m =>
                m.TournamentId == tournament.Id && m.BracketSide == BracketSide.Losers && m.NextMatchId == gf1.Id);
            if (lbFinal?.WinnerTeamId is not null)
            {
                var thirdId = lbFinal.TeamAId == lbFinal.WinnerTeamId ? lbFinal.TeamBId : lbFinal.TeamAId;
                if (thirdId.HasValue)
                {
                    var third = await _db.TournamentTeams.FindAsync(thirdId.Value);
                    if (third != null) third.FinalRank = 3;
                }
            }
        }
        else
        {
            // Single-elimination style: both semifinal losers share 3rd (documented assumption
            // — no decider match). A 2-team bracket has no semifinal, so no 3rd place.
            var semis = await _db.TournamentMatches
                .Where(m => m.TournamentId == tournament.Id && m.NextMatchId == finalMatch.Id)
                .ToListAsync();
            foreach (var s in semis)
            {
                if (s.WinnerTeamId is null) continue;
                var thirdId = s.TeamAId == s.WinnerTeamId ? s.TeamBId : s.TeamAId;
                if (!thirdId.HasValue) continue;
                var third = await _db.TournamentTeams.FindAsync(thirdId.Value);
                if (third != null) third.FinalRank = 3;
            }
        }

        tournament.Status = TournamentStatus.Completed;
        tournament.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task FeedMatchSlotAsync(int matchId, int slot, int teamId)
    {
        var m = await _db.TournamentMatches.FindAsync(matchId);
        if (m is null) return;
        if (slot == 0) m.TeamAId = teamId; else m.TeamBId = teamId;
        if (m.Status == MatchStatus.Pending && m.TeamAId.HasValue && m.TeamBId.HasValue)
            m.Status = MatchStatus.Ready;
    }

    // Materializes a generated bracket plan (list-index-based links) into real TournamentMatch
    // rows (database-id-based links). Two passes: insert every row first (so ids exist), then
    // wire NextMatchId/LoserNextMatchId using those real ids.
    private async Task MaterializeBracketAsync(Tournament tournament, IReadOnlyList<int> orderedTeamIdsBySeed)
    {
        var plan = tournament.Format is TournamentFormat.SingleElimination or TournamentFormat.RoundRobinToSingleElim
            ? _bracket.BuildSingleElimination(orderedTeamIdsBySeed.Count)
            : _bracket.BuildDoubleElimination(orderedTeamIdsBySeed.Count);

        var rows = new TournamentMatch[plan.Count];
        for (var i = 0; i < plan.Count; i++)
        {
            var p = plan[i];
            int? teamAId = p.TeamASeed.HasValue ? orderedTeamIdsBySeed[p.TeamASeed.Value - 1] : null;
            int? teamBId = p.TeamBSeed.HasValue ? orderedTeamIdsBySeed[p.TeamBSeed.Value - 1] : null;
            int? winnerId = p.AutoWinnerSeed.HasValue ? orderedTeamIdsBySeed[p.AutoWinnerSeed.Value - 1] : null;

            var row = new TournamentMatch
            {
                TournamentId = tournament.Id,
                Stage = MatchStage.Bracket,
                BracketSide = p.Side,
                Round = p.Round,
                SlotIndex = p.SlotIndex,
                TeamAId = teamAId,
                TeamBId = teamBId,
                TeamAIsBye = p.TeamAIsBye,
                TeamBIsBye = p.TeamBIsBye,
                WinnerTeamId = winnerId,
                Status = p.Status,
                CompletedAt = winnerId.HasValue ? DateTimeOffset.UtcNow : null,
            };
            rows[i] = row;
            _db.TournamentMatches.Add(row);
        }
        await _db.SaveChangesAsync(); // populate row ids

        for (var i = 0; i < plan.Count; i++)
        {
            var p = plan[i];
            if (p.NextMatchIndex.HasValue)
            {
                rows[i].NextMatchId = rows[p.NextMatchIndex.Value].Id;
                rows[i].NextMatchSlot = p.NextMatchSlot;
            }
            if (p.LoserNextMatchIndex.HasValue)
            {
                rows[i].LoserNextMatchId = rows[p.LoserNextMatchIndex.Value].Id;
                rows[i].LoserNextMatchSlot = p.LoserNextMatchSlot;
            }
        }
        await _db.SaveChangesAsync();
    }
}
