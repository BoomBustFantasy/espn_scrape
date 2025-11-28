using System.Text.Json.Serialization;

namespace ESPNScrape.Models;

public class Game
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;

    // These are typically references in ESPN API responses
    public SeasonReference? Season { get; set; }
    public SeasonTypeReference? SeasonType { get; set; }
    public WeekReference? Week { get; set; }
    public bool TimeValid { get; set; }
    public List<Competition> Competitions { get; set; } = new();
    public List<Link> Links { get; set; } = new();
    public List<VenueReference> Venues { get; set; } = new();
    public LeagueReference? League { get; set; }
}

public class Competition
{
    public string Id { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int Attendance { get; set; }
    public CompetitionType? Type { get; set; }
    public bool TimeValid { get; set; }
    public bool DateValid { get; set; }
    public bool NeutralSite { get; set; }
    public bool DivisionCompetition { get; set; }
    public bool ConferenceCompetition { get; set; }

    // Properties that are typically references
    public VenueReference? Venue { get; set; }
    public WeatherReference? Weather { get; set; }
    public List<Competitor> Competitors { get; set; } = new();
    public OfficialsReference? Officials { get; set; }
    public StatusReference? Status { get; set; }
    public List<Link> Links { get; set; } = new();

    // Additional references found in the API response
    public OddsReference? Odds { get; set; }
    public object? Broadcasts { get; set; }
    public object? Details { get; set; }
    public LeadersReference? Leaders { get; set; }
    public object? Predictor { get; set; }
    public object? Probabilities { get; set; }
    public object? PowerIndexes { get; set; }
    public object? Drives { get; set; }
}

public class Competitor
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Order { get; set; }
    public string HomeAway { get; set; } = string.Empty;
    public bool Winner { get; set; }

    // These are typically references in ESPN API responses
    public TeamReference? Team { get; set; }
    public object? Score { get; set; } // Can be reference or embedded
    public RecordReference? Record { get; set; } // This is a reference, not a list
    public object? Linescores { get; set; } // Reference
    public object? Roster { get; set; } // Reference  
    public object? Statistics { get; set; } // Reference
    public object? Leaders { get; set; } // Reference
}

public class Score
{
    public decimal Value { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
}

public class Record
{
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
}

public class Season
{
    public int Year { get; set; }
    public bool Current { get; set; }
    public int Type { get; set; }
}

public class SeasonType
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
}

public class Week
{
    public int Number { get; set; }
}

public class CompetitionType
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class Official
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Position? Position { get; set; }
    public int Order { get; set; }
}

public class Position
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class Status
{
    public StatusType? Type { get; set; }
    public bool IsTBDFlex { get; set; }
}

public class StatusType
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string ShortDetail { get; set; } = string.Empty;
}

public class Weather
{
    public string Type { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public int WindSpeed { get; set; }
    public string WindDirection { get; set; } = string.Empty;
    public int Temperature { get; set; }
    public int HighTemperature { get; set; }
    public int LowTemperature { get; set; }
    public string ConditionId { get; set; } = string.Empty;
    public int Gust { get; set; }
    public int Precipitation { get; set; }
    public Link? Link { get; set; }
}