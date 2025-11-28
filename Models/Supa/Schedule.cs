using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supa;

[Table("Schedule")]
public class Schedule : BaseModel
{
    [PrimaryKey("id")]
    [JsonPropertyName("id")]
    [Column("id")]
    public long Id { get; set; }

    [JsonPropertyName("espn_game_id")]
    [Column("espn_game_id")]
    public string? EspnGameId { get; set; }

    [JsonPropertyName("home_team_id")]
    [Column("home_team_id")]
    public long? HomeTeamId { get; set; }

    [JsonPropertyName("away_team_id")]
    [Column("away_team_id")]
    public long? AwayTeamId { get; set; }

    [JsonPropertyName("game_time")]
    [Column("game_time")]
    public DateTime GameTime { get; set; }

    [JsonPropertyName("week")]
    [Column("week")]
    public int Week { get; set; }

    [JsonPropertyName("year")]
    [Column("year")]
    public int Year { get; set; }

    [JsonPropertyName("season_type")]
    [Column("season_type")]
    public int SeasonType { get; set; } = 2; // 1 = preseason, 2 = regular season, 3 = playoffs

    [JsonPropertyName("betting_line")]
    [Column("betting_line")]
    public decimal? BettingLine { get; set; }

    [JsonPropertyName("over_under")]
    [Column("over_under")]
    public decimal? OverUnder { get; set; }

    [JsonPropertyName("home_implied_points")]
    [Column("home_implied_points")]
    public decimal? HomeImpliedPoints { get; set; }

    [JsonPropertyName("away_implied_points")]
    [Column("away_implied_points")]
    public decimal? AwayImpliedPoints { get; set; }

    [JsonPropertyName("created_at")]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}