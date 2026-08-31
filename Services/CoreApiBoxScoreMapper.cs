using System.Text.RegularExpressions;
using ESPNScrape.Models;

namespace ESPNScrape.Services;

// Maps the sports.core.api.espn.com competitor/athlete statistics endpoints
// (StatisticsResponse) into the same ParsedPlayerStat shape BoxScoreParser
// produces from the (now-blocked) site.api.espn.com summary endpoint.
public static class CoreApiBoxScoreMapper
{
    private static readonly string[] RelevantCategories = ["passing", "rushing", "receiving"];

    // Union of athlete refs found under the passing/rushing/receiving categories
    // of a competitor's team-level statistics response, deduped by athlete ref URL.
    public static List<StatAthleteRef> ExtractRelevantAthletes(StatisticsResponse teamStats)
    {
        var seen = new Dictionary<string, StatAthleteRef>();

        var categories = teamStats.Splits?.Categories ?? [];
        foreach (var category in categories.Where(c => RelevantCategories.Contains(c.Name.ToLower())))
        {
            if (category.Athletes == null)
                continue;

            foreach (var athleteRef in category.Athletes)
            {
                var url = athleteRef.Athlete?.GetUrl();
                if (string.IsNullOrEmpty(url) || seen.ContainsKey(url))
                    continue;

                seen[url] = athleteRef;
            }
        }

        return [.. seen.Values];
    }

    public static string? ExtractAthleteId(string athleteRefUrl)
    {
        var match = Regex.Match(athleteRefUrl, @"/athletes/(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    public static ParsedPlayerStat Map(
        StatisticsResponse athleteStats,
        string espnPlayerId,
        string espnTeamId,
        string teamDisplayName,
        string name,
        string gameId,
        DateTime gameDate,
        int season,
        int week)
    {
        var categories = athleteStats.Splits?.Categories ?? [];
        var byName = categories.ToDictionary(c => c.Name.ToLower(), c => c);

        var result = new ParsedPlayerStat
        {
            EspnPlayerId = espnPlayerId,
            EspnTeamId = espnTeamId,
            TeamDisplayName = teamDisplayName,
            Name = name,
            EspnGameId = gameId,
            GameDate = gameDate,
            Season = season,
            Week = week,
            Passing = ConvertCategory(byName.GetValueOrDefault("passing"), PassingKeyMap),
            Rushing = ConvertCategory(byName.GetValueOrDefault("rushing"), RushingKeyMap),
            Receiving = ConvertCategory(byName.GetValueOrDefault("receiving"), ReceivingKeyMap),
        };

        var general = byName.GetValueOrDefault("general");
        if (general != null)
        {
            result.Fumbles = (int?)FindStatValue(general, "fumbles") ?? 0;
            result.FumblesLost = (int?)FindStatValue(general, "fumblesLost") ?? 0;
        }

        // Two-point conversions live on the scoring category of each discipline
        // rather than under general, so they are read per-category.
        result.TwoPtPass = ReadTwoPointStat(byName, "passing", "twoPtPass");
        result.TwoPtRush = ReadTwoPointStat(byName, "rushing", "twoPtRush");
        result.TwoPtReception = ReadTwoPointStat(byName, "receiving", "twoPtReception");

        return result;
    }

    private static readonly Dictionary<string, string> PassingKeyMap = new()
    {
        ["completions"] = "completions",
        ["passingYards"] = "passingyards",
        ["interceptions"] = "interceptions",
        ["passingAttempts"] = "passingattempts",
        ["passingTouchdowns"] = "passingtouchdowns",
        ["yardsPerPassAttempt"] = "yardsperpassattempt",
        ["QBRating"] = "qbrating",
        ["ESPNQBRating"] = "adjqbr",
    };

    private static readonly Dictionary<string, string> RushingKeyMap = new()
    {
        ["longRushing"] = "longrushing",
        ["rushingYards"] = "rushingyards",
        ["rushingAttempts"] = "rushingattempts",
        ["rushingTouchdowns"] = "rushingtouchdowns",
    };

    private static readonly Dictionary<string, string> ReceivingKeyMap = new()
    {
        ["receptions"] = "receptions",
        ["longReception"] = "longreception",
        ["receivingYards"] = "receivingyards",
        ["receivingTargets"] = "receivingtargets",
        ["yardsPerReception"] = "yardsperreception",
        ["receivingTouchdowns"] = "receivingtouchdowns",
    };

    private static object? ConvertCategory(StatCategory? category, Dictionary<string, string> keyMap)
    {
        if (category == null)
            return null;

        var result = new Dictionary<string, object>();

        foreach (var (sourceName, targetKey) in keyMap)
        {
            var value = FindStatValue(category, sourceName);
            if (value.HasValue)
                result[targetKey] = value.Value;
        }

        if (result.Count == 0)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    private static int ReadTwoPointStat(Dictionary<string, StatCategory> byName, string categoryName, string statName)
    {
        var category = byName.GetValueOrDefault(categoryName);
        return category == null ? 0 : (int?)FindStatValue(category, statName) ?? 0;
    }

    private static double? FindStatValue(StatCategory category, string statName)
    {
        return category.Stats
            .FirstOrDefault(s => string.Equals(s.Name, statName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }
}
