using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Supa;

/// <summary>
/// Represents passing statistics stored in the PlayerStats.passing JSONB field
/// </summary>
public class PassingStats
{
    [JsonPropertyName("adjqbr")]
    public double? AdjustedQBR { get; set; }

    [JsonPropertyName("qbrating")]
    public double? QBRating { get; set; }

    [JsonPropertyName("completions")]
    public int? Completions { get; set; }

    [JsonPropertyName("passingyards")]
    public int? PassingYards { get; set; }

    [JsonPropertyName("interceptions")]
    public int? Interceptions { get; set; }

    [JsonPropertyName("passingattempts")]
    public int? PassingAttempts { get; set; }

    [JsonPropertyName("passingtouchdowns")]
    public int? PassingTouchdowns { get; set; }

    [JsonPropertyName("yardsperpassattempt")]
    public double? YardsPerPassAttempt { get; set; }

    [JsonPropertyName("yardsperrushattempt")]
    public double? YardsPerRushAttempt { get; set; }

    // Calculated properties
    public double? CompletionPercentage =>
        PassingAttempts > 0 ? (double?)Completions / PassingAttempts * 100 : null;

    public string CompletionAttemptsDisplay =>
        $"{Completions ?? 0}/{PassingAttempts ?? 0}";
}

/// <summary>
/// Represents rushing statistics stored in the PlayerStats.rushing JSONB field
/// </summary>
public class RushingStats
{
    [JsonPropertyName("longrushing")]
    public int? LongRushing { get; set; }

    [JsonPropertyName("rushingyards")]
    public int? RushingYards { get; set; }

    [JsonPropertyName("rushingattempts")]
    public int? RushingAttempts { get; set; }

    [JsonPropertyName("rushingtouchdowns")]
    public int? RushingTouchdowns { get; set; }

    // Calculated properties
    public double? YardsPerCarry =>
        RushingAttempts > 0 ? (double?)RushingYards / RushingAttempts : null;
}

/// <summary>
/// Represents receiving statistics stored in the PlayerStats.receiving JSONB field
/// </summary>
public class ReceivingStats
{
    [JsonPropertyName("receptions")]
    public int? Receptions { get; set; }

    [JsonPropertyName("longreception")]
    public int? LongReception { get; set; }

    [JsonPropertyName("receivingyards")]
    public int? ReceivingYards { get; set; }

    [JsonPropertyName("receivingtargets")]
    public int? ReceivingTargets { get; set; }

    [JsonPropertyName("yardsperreception")]
    public double? YardsPerReception { get; set; }

    [JsonPropertyName("receivingtouchdowns")]
    public int? ReceivingTouchdowns { get; set; }

    // Calculated properties
    public double? CatchPercentage =>
        ReceivingTargets > 0 ? (double?)Receptions / ReceivingTargets * 100 : null;

    public string ReceptionTargetsDisplay =>
        $"{Receptions ?? 0}/{ReceivingTargets ?? 0}";
}