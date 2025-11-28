using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supa;

[Table("Players")]
public class Player : BaseModel
{
    [PrimaryKey("id")]
    [JsonPropertyName("id")]
    [Column("id")]
    public long Id { get; set; }

    [JsonPropertyName("first_name")]
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("team_id")]
    [Column("team_id")]
    public long TeamId { get; set; }

    [JsonPropertyName("position_id")]
    [Column("position_id")]
    public long? PositionId { get; set; }

    [JsonPropertyName("espn_player_id")]
    [Column("espn_player_id")]
    public string? EspnPlayerId { get; set; }

    [JsonPropertyName("fantasy_sharks_player_id")]
    [Column("fantasy_sharks_player_id")]
    public long? FantasySharksPlayerId { get; set; }

    [JsonPropertyName("pro_football_reference_id")]
    [Column("pro_football_reference_id")]
    public long? ProFootballReferenceId { get; set; }

    [JsonPropertyName("sleeper_id")]
    [Column("sleeper_id")]
    public long? SleeperId { get; set; }

    [JsonPropertyName("age")]
    [Column("age")]
    public decimal? Age { get; set; }

    [JsonPropertyName("ktc_player_id")]
    [Column("ktc_player_id")]
    public string? KtcPlayerId { get; set; }

    [JsonPropertyName("ktc_player_link")]
    [Column("ktc_player_link")]
    public string? KtcPlayerLink { get; set; }

    [JsonPropertyName("positional_rank")]
    [Column("positional_rank")]
    public int? PositionalRank { get; set; }

    [JsonPropertyName("overall_rank")]
    [Column("overall_rank")]
    public int? OverallRank { get; set; }

    [JsonPropertyName("ktc_value")]
    [Column("ktc_value")]
    public int? KtcValue { get; set; }

    [JsonPropertyName("fantasy_calc_redraft_value")]
    [Column("fantasy_calc_redraft_value")]
    public int? FantasyCalcRedraftValue { get; set; }

    [JsonPropertyName("active")]
    [Column("active")]
    public bool? Active { get; set; } = false;

    [JsonPropertyName("pfr_player_code")]
    [Column("pfr_player_code")]
    public string? PfrPlayerCode { get; set; }

    [JsonPropertyName("created_at")]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("headshot_url")]
    [Column("headshot_url")]
    public string? HeadshotUrl { get; set; }

    [JsonPropertyName("headshot_alt")]
    [Column("headshot_alt")]
    public string? HeadshotAlt { get; set; }

    [JsonPropertyName("headshot_width")]
    [Column("headshot_width")]
    public int? HeadshotWidth { get; set; }

    [JsonPropertyName("headshot_height")]
    [Column("headshot_height")]
    public int? HeadshotHeight { get; set; }

    [JsonPropertyName("headshot_updated_at")]
    [Column("headshot_updated_at")]
    public DateTime? HeadshotUpdatedAt { get; set; }

    [JsonPropertyName("storage_path")]
    [Column("storage_path")]
    public string? StoragePath { get; set; }

    [JsonPropertyName("headshot_sizes")]
    [Column("headshot_sizes")]
    public string? HeadshotSizes { get; set; }
}