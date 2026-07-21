using LeagueClassic.Web.Data;

namespace LeagueClassic.Web.Pages.Tournaments;

public sealed record GroupStandingsVm(
    string TournamentSlug, TournamentGroup Group, List<TournamentMatch> Matches,
    List<TournamentTeam> StandingsOrder, int AdvancePerGroup, bool CanManage);

public sealed record BracketVm(string TournamentSlug, List<TournamentMatch> Matches, bool CanManage);
