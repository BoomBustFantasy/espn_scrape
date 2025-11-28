using System.Text.Json.Serialization;
using System.Text.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace ESPNScrape.Models.Supa;

[Table("PlayerStats")]
public class PlayerStat : BaseModel
{
    [Column("id")]
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [PrimaryKey("player_code")]
    [JsonPropertyName("player_code")]
    [Column("player_code")]
    public string PlayerCode { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    [Column("team")]
    public string Team { get; set; } = string.Empty;

    [PrimaryKey("game_date")]
    [JsonPropertyName("game_date")]
    [Column("game_date")]
    public DateTime GameDate { get; set; }

    [JsonPropertyName("game_location")]
    [Column("game_location")]
    public string GameLocation { get; set; } = string.Empty;

    [JsonPropertyName("passing")]
    [Column("passing")]
    public object? Passing { get; set; }

    [JsonPropertyName("rushing")]
    [Column("rushing")]
    public object? Rushing { get; set; }

    [JsonPropertyName("receiving")]
    [Column("receiving")]
    public object? Receiving { get; set; }

    [JsonPropertyName("created_at")]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("player_id")]
    [Column("player_id")]
    public long? PlayerId { get; set; }

    [JsonPropertyName("espn_player_id")]
    [Column("espn_player_id")]
    public string? EspnPlayerId { get; set; }

    [JsonPropertyName("espn_game_id")]
    [Column("espn_game_id")]
    public string? EspnGameId { get; set; }

    [JsonPropertyName("season")]
    [Column("season")]
    public int? Season { get; set; }

    [JsonPropertyName("week")]
    [Column("week")]
    public int? Week { get; set; }

    [JsonPropertyName("fumbles")]
    [Column("fumbles")]
    public int? Fumbles { get; set; }

    [JsonPropertyName("fumbles_lost")]
    [Column("fumbles_lost")]
    public int? FumblesLost { get; set; }

    // Helper methods to deserialize JSONB stats with proper typing
    public PassingStats? GetPassingStats()
    {
        if (Passing == null)
            return null;

        try
        {
            var json = Passing.ToString();
            if (string.IsNullOrEmpty(json))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<PassingStats>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public RushingStats? GetRushingStats()
    {
        if (Rushing == null)
            return null;

        try
        {
            var json = Rushing.ToString();
            if (string.IsNullOrEmpty(json))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<RushingStats>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public ReceivingStats? GetReceivingStats()
    {
        if (Receiving == null)
            return null;

        try
        {
            var json = Receiving.ToString();
            if (string.IsNullOrEmpty(json))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<ReceivingStats>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // Generic helper method for custom deserialization
    public T? GetStatsAs<T>(object? jsonObject) where T : class
    {
        if (jsonObject == null)
            return null;

        try
        {
            var json = jsonObject.ToString();
            if (string.IsNullOrEmpty(json))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}