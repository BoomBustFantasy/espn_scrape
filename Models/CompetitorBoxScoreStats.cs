namespace ESPNScrape.Models;

// Shared shape for both the team-level (competitors/{teamId}/statistics) and
// athlete-level (competitors/{teamId}/roster/{athleteId}/statistics/0) endpoints
// on sports.core.api.espn.com. The athlete-level response simply leaves
// StatEntry.Athletes null on every entry.
public class StatisticsResponse
{
    public StatSplits? Splits { get; set; }
}

public class StatSplits
{
    public List<StatCategory> Categories { get; set; } = new();
}

public class StatCategory
{
    public string Name { get; set; } = string.Empty;
    public List<StatEntry> Stats { get; set; } = new();
    public List<StatAthleteRef>? Athletes { get; set; }
}

public class StatEntry
{
    public string Name { get; set; } = string.Empty;
    public double? Value { get; set; }
}

public class StatAthleteRef
{
    public AthletesReference? Athlete { get; set; }
    public StatisticsReference? Statistics { get; set; }
}
