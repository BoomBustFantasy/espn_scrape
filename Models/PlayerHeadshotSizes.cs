using System.Text.Json.Serialization;

namespace ESPNScrape.Models;

/// <summary>
/// Represents different sizes of player headshot images
/// </summary>
public class PlayerHeadshotSizes
{
    [JsonPropertyName("full")]
    public HeadshotSize? Full { get; set; }

    [JsonPropertyName("profile")]
    public HeadshotSize? Profile { get; set; }

    [JsonPropertyName("thumbnail")]
    public HeadshotSize? Thumbnail { get; set; }
}

/// <summary>
/// Individual headshot size information
/// </summary>
public class HeadshotSize
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("storage_path")]
    public string StoragePath { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("file_size")]
    public long FileSize { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// ESPN headshot size configurations
/// </summary>
public static class ESPNHeadshotSizes
{
    public const string Full = "full";
    public const string Profile = "profile";
    public const string Thumbnail = "thumbnail";

    public static readonly Dictionary<string, (int ExpectedWidth, int ExpectedHeight)> SizeExpectations = new()
    {
        { Full, (400, 400) },
        { Profile, (180, 180) },
        { Thumbnail, (65, 65) }
    };

    public static readonly string[] AllSizes = { Full, Profile, Thumbnail };
}