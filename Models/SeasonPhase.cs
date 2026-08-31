namespace ESPNScrape.Models;

/// <summary>
/// Where the NFL calendar currently sits, as reported by ESPN rather than inferred
/// from today's date. Replaces the day-of-month guesswork in <see cref="Utils.NFLCalendar"/>,
/// which has to be retuned every season.
/// </summary>
public record SeasonPhase(int Season, int SeasonType, int Week)
{
    public const int Preseason = 1;
    public const int RegularSeason = 2;
    public const int Postseason = 3;

    public bool IsRegularSeason => SeasonType == RegularSeason;
    public bool IsPostseason => SeasonType == Postseason;
}

// Response shape of https://sports.core.api.espn.com/v2/sports/football/leagues/nfl
// Only the fields needed to locate the current week are modelled.
public class LeagueRootResponse
{
    public LeagueSeason? Season { get; set; }
}

public class LeagueSeason
{
    public int Year { get; set; }
    public LeagueSeasonType? Type { get; set; }
}

public class LeagueSeasonType
{
    public int Type { get; set; }
    public LeagueWeek? Week { get; set; }
}

public class LeagueWeek
{
    public int Number { get; set; }
}
