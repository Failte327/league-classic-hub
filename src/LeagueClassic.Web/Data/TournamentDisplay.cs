namespace LeagueClassic.Web.Data;

public static class TournamentDisplay
{
    public static string DisplayName(this TournamentFormat format) => format switch
    {
        TournamentFormat.SingleElimination => "Single Elimination",
        TournamentFormat.DoubleElimination => "Double Elimination",
        TournamentFormat.RoundRobinToSingleElim => "Round Robin → Single Elim",
        TournamentFormat.RoundRobinToDoubleElim => "Round Robin → Double Elim",
        _ => format.ToString(),
    };

    public static string DisplayName(this TournamentStatus status) => status switch
    {
        TournamentStatus.RegistrationOpen => "Registration Open",
        TournamentStatus.InProgress => "In Progress",
        TournamentStatus.Completed => "Completed",
        _ => status.ToString(),
    };
}
