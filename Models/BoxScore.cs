namespace ESPNScrape.Models;

public class BoxScore
{
    public List<TeamBoxScore> Teams { get; set; } = new();
    public GameFormat? Format { get; set; }
    public GameInfo? GameInfo { get; set; }
    public List<LastFiveGames> LastFiveGames { get; set; } = new();
    public List<TeamLeaders> Leaders { get; set; } = new();
    public List<TeamInjuries> Injuries { get; set; } = new();
    public List<Broadcast> Broadcasts { get; set; } = new();
    public List<PickCenter> Pickcenter { get; set; } = new();
    public List<AgainstTheSpread> AgainstTheSpread { get; set; } = new();
    public List<Odds> Odds { get; set; } = new();
    public News? News { get; set; }
    public Predictor? Predictor { get; set; }
    public List<decimal> Winprobability { get; set; } = new();
    public BoxScoreHeader? Header { get; set; }
}

public class TeamBoxScore
{
    public Team? Team { get; set; }
    public List<Statistic> Statistics { get; set; } = new();
    public int DisplayOrder { get; set; }
    public string HomeAway { get; set; } = string.Empty;
}

public class Statistic
{
    public string Name { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class GameFormat
{
    public Period? Regulation { get; set; }
    public Period? Overtime { get; set; }
}

public class Period
{
    public int Periods { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Clock { get; set; }
}

public class GameInfo
{
    public Venue? Venue { get; set; }
    public Weather? Weather { get; set; }
    public List<Official> Officials { get; set; } = new();
}

public class LastFiveGames
{
    public int DisplayOrder { get; set; }
    public Team? Team { get; set; }
    public List<RecentGame> Events { get; set; } = new();
}

public class RecentGame
{
    public string Id { get; set; } = string.Empty;
    public List<Link> Links { get; set; } = new();
    public int Week { get; set; }
    public string AtVs { get; set; } = string.Empty;
    public DateTime GameDate { get; set; }
    public string Score { get; set; } = string.Empty;
    public string HomeTeamId { get; set; } = string.Empty;
    public string AwayTeamId { get; set; } = string.Empty;
    public string HomeTeamScore { get; set; } = string.Empty;
    public string AwayTeamScore { get; set; } = string.Empty;
    public string HomeAggregateScore { get; set; } = string.Empty;
    public string AwayAggregateScore { get; set; } = string.Empty;
    public string HomeShootoutScore { get; set; } = string.Empty;
    public string AwayShootoutScore { get; set; } = string.Empty;
    public string GameResult { get; set; } = string.Empty;
    public Team? Opponent { get; set; }
    public string OpponentLogo { get; set; } = string.Empty;
    public string LeagueName { get; set; } = string.Empty;
    public string LeagueAbbreviation { get; set; } = string.Empty;
}

public class TeamLeaders
{
    public Team? Team { get; set; }
    public List<StatLeader> Leaders { get; set; } = new();
}

public class StatLeader
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<PlayerStat> Leaders { get; set; } = new();
}

public class PlayerStat
{
    public string DisplayValue { get; set; } = string.Empty;
    public Player? Athlete { get; set; }
    public MainStat? MainStat { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class MainStat
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class TeamInjuries
{
    public Team? Team { get; set; }
    public List<Injury> Injuries { get; set; } = new();
}

public class Injury
{
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public Player? Athlete { get; set; }
    public InjuryType? Type { get; set; }
    public InjuryDetails? Details { get; set; }
}

public class InjuryType
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
}

public class InjuryDetails
{
    public FantasyStatus? FantasyStatus { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public DateTime? ReturnDate { get; set; }
}

public class FantasyStatus
{
    public string Description { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string DisplayDescription { get; set; } = string.Empty;
}

public class Broadcast
{
    public BroadcastType? Type { get; set; }
    public Market? Market { get; set; }
    public Media? Media { get; set; }
    public string Lang { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsNational { get; set; }
}

public class BroadcastType
{
    public string Id { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
}

public class Market
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class Media
{
    public string ShortName { get; set; } = string.Empty;
}

public class PickCenter
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
    public MoneyLine? MoneyLine { get; set; }
    public PointSpreadBet? PointSpread { get; set; }
    public Total? Total { get; set; }
    public Link? Link { get; set; }
    public Header? Header { get; set; }
}

public class MoneyLine
{
    public string DisplayName { get; set; } = string.Empty;
    public string ShortDisplayName { get; set; } = string.Empty;
    public MoneyLineTeam? Home { get; set; }
    public MoneyLineTeam? Away { get; set; }
}

public class MoneyLineTeam
{
    public MoneyLineOdds? Close { get; set; }
    public MoneyLineOdds? Open { get; set; }
}

public class MoneyLineOdds
{
    public string Odds { get; set; } = string.Empty;
    public Link? Link { get; set; }
}

public class PointSpreadBet
{
    public string DisplayName { get; set; } = string.Empty;
    public string ShortDisplayName { get; set; } = string.Empty;
    public SpreadTeam? Home { get; set; }
    public SpreadTeam? Away { get; set; }
}

public class SpreadTeam
{
    public SpreadOdds? Close { get; set; }
    public SpreadOdds? Open { get; set; }
}

public class SpreadOdds
{
    public string Line { get; set; } = string.Empty;
    public string Odds { get; set; } = string.Empty;
    public Link? Link { get; set; }
}

public class Total
{
    public string DisplayName { get; set; } = string.Empty;
    public string ShortDisplayName { get; set; } = string.Empty;
    public TotalBet? Over { get; set; }
    public TotalBet? Under { get; set; }
}

public class TotalBet
{
    public TotalOdds? Close { get; set; }
    public TotalOdds? Open { get; set; }
}

public class TotalOdds
{
    public string Line { get; set; } = string.Empty;
    public string Odds { get; set; } = string.Empty;
    public Link? Link { get; set; }
}

public class Header
{
    public Logo? Logo { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class AgainstTheSpread
{
    public Team? Team { get; set; }
    public List<Record> Records { get; set; } = new();
}

public class News
{
    public string Header { get; set; } = string.Empty;
    public Link? Link { get; set; }
    public List<Article> Articles { get; set; } = new();
}

public class Article
{
    public string Id { get; set; } = string.Empty;
    public string NowId { get; set; } = string.Empty;
    public string ContentKey { get; set; } = string.Empty;
    public string DataSourceIdentifier { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public DateTime Published { get; set; }
    public List<Image> Images { get; set; } = new();
    public bool Premium { get; set; }
    public ArticleLinks? Links { get; set; }
    public string Byline { get; set; } = string.Empty;
}

public class ArticleLinks
{
    public WebLink? Web { get; set; }
    public MobileLink? Mobile { get; set; }
    public ApiLink? Api { get; set; }
    public AppLink? App { get; set; }
}

public class WebLink
{
    public string Href { get; set; } = string.Empty;
    public SelfLink? Self { get; set; }
    public SeoLink? Seo { get; set; }
}

public class SelfLink
{
    public string Href { get; set; } = string.Empty;
    public string Dsi { get; set; } = string.Empty;
}

public class SeoLink
{
    public string Href { get; set; } = string.Empty;
}

public class MobileLink
{
    public string Href { get; set; } = string.Empty;
}

public class ApiLink
{
    public SelfLink? Self { get; set; }
    public ArtworkLink? Artwork { get; set; }
}

public class ArtworkLink
{
    public string Href { get; set; } = string.Empty;
}

public class AppLink
{
    public SportsCenterLink? Sportscenter { get; set; }
}

public class SportsCenterLink
{
    public string Href { get; set; } = string.Empty;
}

public class Predictor
{
    public string Header { get; set; } = string.Empty;
    public PredictorTeam? HomeTeam { get; set; }
    public PredictorTeam? AwayTeam { get; set; }
}

public class PredictorTeam
{
    public string Id { get; set; } = string.Empty;
    public string GameProjection { get; set; } = string.Empty;
    public string TeamChanceLoss { get; set; } = string.Empty;
}

public class BoxScoreHeader
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public Season? Season { get; set; }
    public bool TimeValid { get; set; }
    public List<Competition> Competitions { get; set; } = new();
    public List<Link> Links { get; set; } = new();
    public int Week { get; set; }
    public League? League { get; set; }
}