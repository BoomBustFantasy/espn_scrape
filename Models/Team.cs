namespace ESPNScrape.Models;

public class Team
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortDisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string AlternateColor { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAllStar { get; set; }
    public List<Logo> Logos { get; set; } = new();
    public List<Link> Links { get; set; } = new();

    // These properties are typically references in ESPN API responses
    public VenueReference? Venue { get; set; }
    public RecordReference? Record { get; set; }
    public AthletesReference? Athletes { get; set; }
    public EventsReference? Events { get; set; }
    public LeadersReference? Leaders { get; set; }
    public StatisticsReference? Statistics { get; set; }
    public FranchiseReference? Franchise { get; set; }
    public LeagueReference? DefaultLeague { get; set; }
    public GroupsReference? Groups { get; set; }
}

public class Logo
{
    public string Href { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Alt { get; set; } = string.Empty;
    public List<string> Rel { get; set; } = new();
    public DateTime? LastUpdated { get; set; }
}

public class Link
{
    public string Language { get; set; } = string.Empty;
    public List<string> Rel { get; set; } = new();
    public string Href { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string ShortText { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
    public bool IsPremium { get; set; }
}