using LeagueClassic.Web.Data;

namespace LeagueClassic.Web.Services;

// Pure bracket/schedule generation — no ApplicationDbContext dependency, so it can be
// hand-verified in isolation before any page depends on it (this is the highest-risk part of
// the Tournaments feature). Inputs/outputs use 1-based "seed numbers" or 0-based "team slot
// indices", never database ids — TournamentService maps these onto real TournamentTeam rows
// (via TournamentTeam.Seed) and materializes the plan into real TournamentMatch rows.
public class TournamentBracketService
{
    // Standard bracket seed placement so top seeds meet as late as possible.
    // Size 8 -> [1,8,4,5,2,7,3,6] (the published standard 8-slot seeding table).
    public static IReadOnlyList<int> SeedOrder(int bracketSize)
    {
        var list = new List<int> { 1 };
        while (list.Count < bracketSize)
        {
            var k = list.Count;
            var next = new List<int>(k * 2);
            foreach (var s in list)
            {
                next.Add(s);
                next.Add(2 * k + 1 - s);
            }
            list = next;
        }
        return list;
    }

    public static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n) p *= 2;
        return p;
    }

    public IReadOnlyList<BracketMatchPlan> BuildSingleElimination(int teamCount)
    {
        if (teamCount < 2) throw new ArgumentException("Need at least 2 teams.", nameof(teamCount));
        var (builders, _, _) = BuildWinnersBracket(teamCount, wireLoserDrops: false);
        return builders.Select(b => b.ToPlan()).ToList();
    }

    public IReadOnlyList<BracketMatchPlan> BuildDoubleElimination(int teamCount)
    {
        if (teamCount < 2) throw new ArgumentException("Need at least 2 teams.", nameof(teamCount));
        var (builders, roundStart, matchesInRound) = BuildWinnersBracket(teamCount, wireLoserDrops: true);
        var rounds = matchesInRound.Length - 1; // matchesInRound is 1-indexed, length = rounds+1

        if (rounds == 1)
        {
            // Degenerate 2-team case: no losers bracket. Grand Final is wired directly from
            // the sole WB match (winner) and its loser (via LoserNextMatch, set below).
            WireGrandFinal(builders, wbFinalIndex: roundStart[1], lbFinalIndex: null);
            return builders.Select(b => b.ToPlan()).ToList();
        }

        // Losers bracket: 2*(rounds-1) rounds. Odd "minor" rounds pair up LB survivors;
        // even "major/drop" rounds pair each LB survivor against that round's WB dropper.
        var lbRoundStart = new int[2 * (rounds - 1) + 1];
        var lbMatchesInRound = new int[2 * (rounds - 1) + 1];
        var bracketSize = matchesInRound[1] * 2;
        for (var j = 1; j <= rounds - 1; j++)
        {
            var count = bracketSize / (int)Math.Pow(2, j + 1);
            lbMatchesInRound[2 * j - 1] = count; // minor round m = 2j-1
            lbMatchesInRound[2 * j] = count;     // major/drop round m = 2j
        }

        var lbBuilders = new List<MatchBuilder>();
        for (var m = 1; m <= 2 * (rounds - 1); m++)
        {
            lbRoundStart[m] = lbBuilders.Count;
            for (var slot = 0; slot < lbMatchesInRound[m]; slot++)
                lbBuilders.Add(new MatchBuilder { Side = BracketSide.Losers, Round = m, SlotIndex = slot });
        }

        // Wire minor round m -> next round (major, m+1): survivors of a minor round feed
        // directly into the paired major round at the same slot index (1:1, no halving yet).
        // Wire major round m -> next round (minor, m+1): survivors pair adjacent (halving).
        for (var m = 1; m < 2 * (rounds - 1); m++)
        {
            var isMinor = m % 2 == 1;
            for (var slot = 0; slot < lbMatchesInRound[m]; slot++)
            {
                var idx = lbRoundStart[m] + slot;
                if (isMinor)
                {
                    lbBuilders[idx].NextMatchIndex = lbRoundStart[m + 1] + slot;
                    lbBuilders[idx].NextMatchSlot = 0; // minor-round winner always fills slot 0; the WB dropper fills slot 1
                }
                else
                {
                    lbBuilders[idx].NextMatchIndex = lbRoundStart[m + 1] + slot / 2;
                    lbBuilders[idx].NextMatchSlot = slot % 2;
                }
            }
        }

        // Wire each WB round's losers into the correct LB drop point.
        // WB round 1 losers -> LB round 1 (minor), paired adjacent (input 2i,2i+1 -> match i).
        for (var slot = 0; slot < matchesInRound[1]; slot++)
        {
            var wbIdx = roundStart[1] + slot;
            var lbIdx = lbRoundStart[1] + slot / 2;
            var lbSlot = slot % 2;
            builders[wbIdx].LoserNextMatchIndex = lbIdx;
            builders[wbIdx].LoserNextMatchSlot = lbSlot;
        }
        // WB round r (r >= 2) losers -> LB major round 2*(r-1), slot i -> slot i (1:1, fills slot 1).
        for (var r = 2; r <= rounds; r++)
        {
            var majorRound = 2 * (r - 1);
            for (var slot = 0; slot < matchesInRound[r]; slot++)
            {
                var wbIdx = roundStart[r] + slot;
                var lbIdx = lbRoundStart[majorRound] + slot;
                builders[wbIdx].LoserNextMatchIndex = lbIdx;
                builders[wbIdx].LoserNextMatchSlot = 1;
            }
        }

        // Re-run bye propagation now that loser-drop links exist (round-1 WB byes need to
        // push a phantom bye into the losers bracket too).
        for (var slot = 0; slot < matchesInRound[1]; slot++)
            ResolveIfReady(builders, roundStart[1] + slot);

        var all = new List<MatchBuilder>(builders);
        var lbOffset = all.Count;
        foreach (var lb in lbBuilders)
        {
            if (lb.NextMatchIndex.HasValue) lb.NextMatchIndex += lbOffset;
            all.Add(lb);
        }
        // fix WB LoserNextMatchIndex to point into the combined list
        foreach (var wb in builders)
            if (wb.LoserNextMatchIndex.HasValue) wb.LoserNextMatchIndex += lbOffset;

        var lbFinalIndex = lbOffset + lbRoundStart[2 * (rounds - 1)];
        WireGrandFinal(all, wbFinalIndex: roundStart[rounds], lbFinalIndex: lbFinalIndex);

        return all.Select(b => b.ToPlan()).ToList();
    }

    // Group count/sizes for a given registered team count and the organizer's configured
    // "teams per group" cap. Sizes are as even as possible (never differ by more than 1) and
    // never exceed TeamsPerGroup.
    public (int groupCount, int[] groupSizes) PlanGroups(int teamCount, int teamsPerGroup)
    {
        if (teamCount < 1 || teamsPerGroup < 2) throw new ArgumentException("Invalid group planning input.");
        var groupCount = (int)Math.Ceiling(teamCount / (double)teamsPerGroup);
        var baseSize = teamCount / groupCount;
        var remainder = teamCount % groupCount;
        var sizes = new int[groupCount];
        for (var i = 0; i < groupCount; i++) sizes[i] = baseSize + (i < remainder ? 1 : 0);
        return (groupCount, sizes);
    }

    // Standard "circle method" round-robin schedule for a single group: fix position 0, rotate
    // the rest each round. Positions are 0-based indices into the caller's team list for that
    // group; the caller maps them onto real team ids. Odd group sizes get one phantom bye
    // position per round (that team simply sits out — no match row, unlike elimination byes).
    public IReadOnlyList<GroupMatchPlan> BuildRoundRobinSchedule(int groupSize)
    {
        if (groupSize < 2) return Array.Empty<GroupMatchPlan>();
        var odd = groupSize % 2 != 0;
        var gPrime = odd ? groupSize + 1 : groupSize;
        var byePos = odd ? gPrime - 1 : -1;
        var positions = Enumerable.Range(0, gPrime).ToList();
        var result = new List<GroupMatchPlan>();

        for (var r = 1; r <= gPrime - 1; r++)
        {
            for (var p = 0; p < gPrime / 2; p++)
            {
                var a = positions[p];
                var b = positions[gPrime - 1 - p];
                if (a == byePos || b == byePos) continue;
                result.Add(new GroupMatchPlan(r, a, b));
            }
            var last = positions[^1];
            for (var i = gPrime - 1; i > 1; i--) positions[i] = positions[i - 1];
            positions[1] = last;
        }
        return result;
    }

    // ---- internals ----

    private static (List<MatchBuilder> builders, int[] roundStart, int[] matchesInRound) BuildWinnersBracket(
        int teamCount, bool wireLoserDrops)
    {
        var bracketSize = NextPowerOfTwo(teamCount);
        var order = SeedOrder(bracketSize);
        var rounds = (int)Math.Round(Math.Log2(bracketSize));

        var builders = new List<MatchBuilder>();
        var roundStart = new int[rounds + 1];
        var matchesInRound = new int[rounds + 1];
        for (var r = 1; r <= rounds; r++) matchesInRound[r] = bracketSize / (1 << r);

        for (var r = 1; r <= rounds; r++)
        {
            roundStart[r] = builders.Count;
            for (var slot = 0; slot < matchesInRound[r]; slot++)
                builders.Add(new MatchBuilder { Side = BracketSide.Winners, Round = r, SlotIndex = slot });
        }
        for (var r = 1; r < rounds; r++)
            for (var slot = 0; slot < matchesInRound[r]; slot++)
            {
                var m = builders[roundStart[r] + slot];
                m.NextMatchIndex = roundStart[r + 1] + slot / 2;
                m.NextMatchSlot = slot % 2;
            }

        for (var i = 0; i < bracketSize; i++)
        {
            var seedNum = order[i];
            var isBye = seedNum > teamCount;
            var matchIdx = roundStart[1] + i / 2;
            var slotInMatch = i % 2;
            FeedSlot(builders, matchIdx, slotInMatch, isBye ? null : seedNum, isBye);
        }

        return (builders, roundStart, matchesInRound);
    }

    private static void WireGrandFinal(List<MatchBuilder> all, int wbFinalIndex, int? lbFinalIndex)
    {
        var gf = new MatchBuilder { Side = BracketSide.GrandFinal, Round = 1, SlotIndex = 0 };
        var gfIndex = all.Count;
        all.Add(gf);

        var wbFinal = all[wbFinalIndex];
        wbFinal.NextMatchIndex = gfIndex;
        wbFinal.NextMatchSlot = 0;

        if (lbFinalIndex.HasValue)
        {
            all[lbFinalIndex.Value].NextMatchIndex = gfIndex;
            all[lbFinalIndex.Value].NextMatchSlot = 1;
        }
        else
        {
            // Degenerate 2-team double-elim: the WB final's loser goes straight to the grand
            // final's other slot (guaranteed rematch — the loser gets one more shot).
            wbFinal.LoserNextMatchIndex = gfIndex;
            wbFinal.LoserNextMatchSlot = 1;
        }

        // If the WB match was itself bye-resolved (can only happen for teamCount==1, already
        // rejected above) there is nothing further to cascade here.
        ResolveIfReady(all, wbFinalIndex);
    }

    private static void FeedSlot(List<MatchBuilder> plans, int matchIndex, int slot, int? seed, bool isBye)
    {
        var m = plans[matchIndex];
        if (slot == 0) { m.TeamASeed = seed; m.TeamAIsBye = isBye; }
        else { m.TeamBSeed = seed; m.TeamBIsBye = isBye; }
        ResolveIfReady(plans, matchIndex);
    }

    private static void ResolveIfReady(List<MatchBuilder> plans, int matchIndex)
    {
        var m = plans[matchIndex];
        var aKnown = m.TeamASeed.HasValue || m.TeamAIsBye;
        var bKnown = m.TeamBSeed.HasValue || m.TeamBIsBye;
        if (!aKnown || !bKnown || m.WinnerSeed.HasValue) return;
        if (m.TeamAIsBye && m.TeamBIsBye) return; // degenerate: nobody to advance, leave unresolved

        if (m.TeamAIsBye) m.WinnerSeed = m.TeamBSeed;
        else if (m.TeamBIsBye) m.WinnerSeed = m.TeamASeed;
        if (!m.WinnerSeed.HasValue) return; // real head-to-head match; winner unknown until played

        if (m.NextMatchIndex.HasValue)
            FeedSlot(plans, m.NextMatchIndex.Value, m.NextMatchSlot!.Value, m.WinnerSeed, false);
        // A bye match has no real loser — propagate a phantom bye into the loser-drop target.
        if (m.LoserNextMatchIndex.HasValue)
            FeedSlot(plans, m.LoserNextMatchIndex.Value, m.LoserNextMatchSlot!.Value, null, true);
    }

    private sealed class MatchBuilder
    {
        public required BracketSide Side { get; init; }
        public required int Round { get; init; }
        public required int SlotIndex { get; init; }
        public int? TeamASeed;
        public int? TeamBSeed;
        public bool TeamAIsBye;
        public bool TeamBIsBye;
        public int? WinnerSeed;
        public int? NextMatchIndex;
        public int? NextMatchSlot;
        public int? LoserNextMatchIndex;
        public int? LoserNextMatchSlot;

        public BracketMatchPlan ToPlan()
        {
            var status = WinnerSeed.HasValue ? MatchStatus.Completed
                : (TeamASeed.HasValue || TeamAIsBye) && (TeamBSeed.HasValue || TeamBIsBye) ? MatchStatus.Ready
                : MatchStatus.Pending;
            return new BracketMatchPlan(Side, Round, SlotIndex, TeamASeed, TeamBSeed, TeamAIsBye, TeamBIsBye,
                WinnerSeed, status, NextMatchIndex, NextMatchSlot, LoserNextMatchIndex, LoserNextMatchSlot);
        }
    }
}

// A generated bracket match, referencing 1-based seed numbers (resolved by the caller against
// TournamentTeam.Seed) and 0-based indices into the returned plan list (resolved by the caller
// into real TournamentMatch ids once rows are inserted).
public sealed record BracketMatchPlan(
    BracketSide Side, int Round, int SlotIndex,
    int? TeamASeed, int? TeamBSeed, bool TeamAIsBye, bool TeamBIsBye, int? AutoWinnerSeed,
    MatchStatus Status,
    int? NextMatchIndex, int? NextMatchSlot,
    int? LoserNextMatchIndex, int? LoserNextMatchSlot);

// One round-robin group-stage pairing, referencing 0-based positions within a single group's
// team list (the caller maps these onto real team ids).
public sealed record GroupMatchPlan(int GroupRound, int TeamIndexA, int TeamIndexB);
