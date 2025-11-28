using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESPNScrape.Models;

// For handling properties that could either be references or embedded objects
public class ESPNReferenceOrData<T> where T : class
{
    [JsonPropertyName("$ref")]
    public string? Ref { get; set; }

    // The actual data if it's embedded instead of referenced
    public T? Data { get; set; }

    public bool IsReference => !string.IsNullOrEmpty(Ref);
    public string GetReferenceUrl() => Ref ?? string.Empty;
}

// Specific reference types for common ESPN entities
public class TeamReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class VenueReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class RecordReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class AthletesReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class EventsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class LeadersReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class StatisticsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class FranchiseReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class LeagueReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class GroupsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

// Additional references for games and competitions
public class SeasonReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class SeasonTypeReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class WeekReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class CompetitionTypeReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class WeatherReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class StatusReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class OfficialsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class BoxScoreReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class PlayByPlayReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class SummaryReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class PicksReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class PredictionsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}

public class OddsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}