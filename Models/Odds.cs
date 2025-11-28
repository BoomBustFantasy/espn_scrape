namespace ESPNScrape.Models;

public class Odds
{
    public Provider? Provider { get; set; }
    public string Details { get; set; } = string.Empty;
    public decimal OverUnder { get; set; }
    public decimal Spread { get; set; }
    public decimal OverOdds { get; set; }
    public decimal UnderOdds { get; set; }
    public TeamOdds? AwayTeamOdds { get; set; }
    public TeamOdds? HomeTeamOdds { get; set; }
    public List<Link> Links { get; set; } = new();
    public bool MoneylineWinner { get; set; }
    public bool SpreadWinner { get; set; }
    public OddsOpen? Open { get; set; }
    public OddsCurrent? Current { get; set; }
}

public class Provider
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public List<Logo> Logos { get; set; } = new();
}

public class TeamOdds
{
    public bool Favorite { get; set; }
    public bool Underdog { get; set; }
    public decimal MoneyLine { get; set; }
    public decimal SpreadOdds { get; set; }
    public OddsOpen? Open { get; set; }
    public OddsCurrent? Current { get; set; }
    public Team? Team { get; set; }
}

public class OddsOpen
{
    public OddsValue? Over { get; set; }
    public OddsValue? Under { get; set; }
    public OddsValue? Total { get; set; }
    public bool Favorite { get; set; }
    public PointSpread? PointSpread { get; set; }
    public OddsValue? Spread { get; set; }
    public OddsValue? MoneyLine { get; set; }
}

public class OddsCurrent
{
    public OddsValue? Over { get; set; }
    public OddsValue? Under { get; set; }
    public OddsValue? Total { get; set; }
    public PointSpread? PointSpread { get; set; }
    public OddsValue? Spread { get; set; }
    public OddsValue? MoneyLine { get; set; }
}

public class OddsValue
{
    public decimal Value { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public string AlternateDisplayValue { get; set; } = string.Empty;
    public decimal Decimal { get; set; }
    public string Fraction { get; set; } = string.Empty;
    public string American { get; set; } = string.Empty;
    public Outcome? Outcome { get; set; }
}

public class PointSpread
{
    public string AlternateDisplayValue { get; set; } = string.Empty;
    public string American { get; set; } = string.Empty;
}

public class Outcome
{
    public string Type { get; set; } = string.Empty;
}