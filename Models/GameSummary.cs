using System.Text.Json.Serialization;
using ESPNScrape.Converters;

namespace ESPNScrape.Models;

public class GameSummary
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public GameBoxScore? BoxScore { get; set; }
    public List<Team> Teams { get; set; } = new();
    public List<Competition> Competitions { get; set; } = new();
}

public class GameBoxScore
{
    public List<BoxScoreTeam> Teams { get; set; } = new();
    public List<TeamPlayerStats> Players { get; set; } = new();
}

public class BoxScoreTeam
{
    public TeamReference? Team { get; set; }
    public List<TeamStatistic> Statistics { get; set; } = new();
}

public class TeamPlayerStats
{
    public TeamBasicInfo? Team { get; set; }
    public List<PlayerStatCategory> Statistics { get; set; } = new();
    public int DisplayOrder { get; set; }
}

public class TeamBasicInfo
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortDisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string AlternateColor { get; set; } = string.Empty;
    public List<Logo> Logos { get; set; } = new();
}

public class PlayerStatCategory
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<string> Keys { get; set; } = new();
    public List<string> Labels { get; set; } = new();
    public List<string> Descriptions { get; set; } = new();
    public List<PlayerStats> Athletes { get; set; } = new();
    public List<string> Totals { get; set; } = new();
}

public class PlayerStats
{
    public PlayerInfo Athlete { get; set; } = new();
    public List<string> Stats { get; set; } = new();
}

public class PlayerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Jersey { get; set; } = string.Empty;
    public List<Link> Links { get; set; } = new();
    public PlayerHeadshot? Headshot { get; set; }
    public PlayerPosition? Position { get; set; }
    public TeamReference? Team { get; set; }
}

public class PlayerHeadshot
{
    public string Href { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
}



public class TeamStatistic
{
    public string Name { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;

    [JsonConverter(typeof(ESPNNumericConverter))]
    public double Value { get; set; }

    public string Label { get; set; } = string.Empty;
}