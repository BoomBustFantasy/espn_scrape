using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supa;

[Table("Teams")]
public class Team : BaseModel
{
    [PrimaryKey("id")]
    [JsonPropertyName("id")]
    [Column("id")]
    public int Id { get; set; }

    [JsonPropertyName("abbreviation")]
    [Column("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public List<Player> Players { get; set; } = new();
    public List<Schedule> HomeGames { get; set; } = new();
    public List<Schedule> AwayGames { get; set; } = new();
    public List<PlayerStat> PlayerStats { get; set; } = new();
}