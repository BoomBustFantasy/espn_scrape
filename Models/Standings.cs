namespace ESPNScrape.Models;

public class Standings
{
    public Link? FullViewLink { get; set; }
    public string Header { get; set; } = string.Empty;
    public List<StandingsGroup> Groups { get; set; } = new();
    public bool IsSameConference { get; set; }
}

public class StandingsGroup
{
    public StandingsTable? Standings { get; set; }
    public string Header { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string ConferenceHeader { get; set; } = string.Empty;
    public string DivisionHeader { get; set; } = string.Empty;
}

public class StandingsTable
{
    public List<StandingsEntry> Entries { get; set; } = new();
}

public class StandingsEntry
{
    public string Team { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public List<StandingsStat> Stats { get; set; } = new();
    public List<Logo> Logo { get; set; } = new();
}

public class StandingsStat
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortDisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}