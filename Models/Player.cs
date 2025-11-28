namespace ESPNScrape.Models;

public class Player
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string DisplayWeight { get; set; } = string.Empty;
    public decimal Height { get; set; }
    public string DisplayHeight { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public int DebutYear { get; set; }
    public List<Link> Links { get; set; } = new();
    public BirthPlace? BirthPlace { get; set; }
    public string Slug { get; set; } = string.Empty;
    public Headshot? Headshot { get; set; }
    public string Jersey { get; set; } = string.Empty;
    public PlayerPosition? Position { get; set; }
    public bool Linked { get; set; }
    public Team? Team { get; set; }
    public Experience? Experience { get; set; }
    public bool Active { get; set; }
    public Draft? Draft { get; set; }
    public PlayerStatus? Status { get; set; }
}

public class BirthPlace
{
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Headshot
{
    public string Href { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
}

public class PlayerPosition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public bool Leaf { get; set; }
}

public class Experience
{
    public int Years { get; set; }
}

public class Draft
{
    public string DisplayText { get; set; } = string.Empty;
    public int Round { get; set; }
    public int Year { get; set; }
    public int Selection { get; set; }
    public Team? Team { get; set; }
}

public class PlayerStatus
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
}