using ESPNScrape.Models;

namespace ESPNScrape.Services;

public static class BoxScoreParser
{
    private static readonly string[] DesiredCategories = ["passing", "rushing", "receiving", "interceptions", "fumbles"];

    public static IReadOnlyList<ParsedPlayerStat> Parse(GameSummary summary, int season, int week)
    {
        if (summary?.BoxScore?.Players == null || summary.BoxScore.Players.Count == 0)
            return [];

        var resultsByPlayerId = new Dictionary<string, ParsedPlayerStat>();

        foreach (var teamPlayerStats in summary.BoxScore.Players)
        {
            var espnTeamId = teamPlayerStats.Team?.Id ?? string.Empty;
            var teamDisplayName = teamPlayerStats.Team?.DisplayName ?? string.Empty;

            if (teamPlayerStats.Statistics == null)
                continue;

            var relevantStats = teamPlayerStats.Statistics
                .Where(s => DesiredCategories.Contains(s.Name.ToLower()))
                .ToList();

            foreach (var statCategory in relevantStats)
            {
                if (statCategory.Athletes == null)
                    continue;

                foreach (var playerStats in statCategory.Athletes)
                {
                    var espnPlayerId = playerStats.Athlete?.Id ?? string.Empty;
                    if (string.IsNullOrEmpty(espnPlayerId))
                        continue;

                    if (!resultsByPlayerId.TryGetValue(espnPlayerId, out var parsedStat))
                    {
                        parsedStat = new ParsedPlayerStat
                        {
                            EspnPlayerId = espnPlayerId,
                            EspnTeamId = espnTeamId,
                            Name = playerStats.Athlete?.DisplayName ?? string.Empty,
                            TeamDisplayName = teamDisplayName,
                            EspnGameId = summary.Id,
                            GameDate = summary.Date,
                            Season = season,
                            Week = week,
                            Fumbles = 0,
                            FumblesLost = 0
                        };
                        resultsByPlayerId[espnPlayerId] = parsedStat;
                    }

                    switch (statCategory.Name.ToLower())
                    {
                        case "passing":
                            parsedStat.Passing = ConvertToPassingJson(statCategory, playerStats);
                            break;
                        case "rushing":
                            parsedStat.Rushing = ConvertToRushingJson(statCategory, playerStats);
                            break;
                        case "receiving":
                            parsedStat.Receiving = ConvertToReceivingJson(statCategory, playerStats);
                            break;
                        case "fumbles":
                            ExtractFumbleStats(statCategory, playerStats, parsedStat);
                            break;
                    }
                }
            }
        }

        return [.. resultsByPlayerId.Values];
    }

    private static object? ConvertToPassingJson(PlayerStatCategory statCategory, PlayerStats playerStatsData)
    {
        var passingStats = new Dictionary<string, object>();

        for (int i = 0; i < statCategory.Keys.Count && i < playerStatsData.Stats.Count; i++)
        {
            var key = statCategory.Keys[i].ToLower();
            var value = playerStatsData.Stats[i];

            switch (key)
            {
                case "completions/passingattempts":
                case "completions/attempts":
                    var parts = value.Split('/');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out var completions))
                            passingStats["completions"] = completions;
                        if (int.TryParse(parts[1], out var attempts))
                            passingStats["passingattempts"] = attempts;
                    }
                    break;
                case "passingyards":
                case "yds":
                    if (int.TryParse(value, out var yards))
                        passingStats["passingyards"] = yards;
                    break;
                case "yardsperpassattempt":
                case "avg":
                    if (double.TryParse(value, out var avgYards))
                        passingStats["yardsperpassattempt"] = avgYards;
                    break;
                case "passingtouchdowns":
                case "td":
                    if (int.TryParse(value, out var tds))
                        passingStats["passingtouchdowns"] = tds;
                    break;
                case "interceptions":
                case "int":
                    if (int.TryParse(value, out var ints))
                        passingStats["interceptions"] = ints;
                    break;
                case "sacks-sackyardslost":
                case "sacks":
                    var sackParts = value.Split('-');
                    if (sackParts.Length == 2)
                    {
                        if (int.TryParse(sackParts[0], out var sacks))
                            passingStats["sacks"] = sacks;
                        if (int.TryParse(sackParts[1], out var sackYards))
                            passingStats["sackyardslost"] = sackYards;
                    }
                    break;
                case "adjqbr":
                    if (double.TryParse(value, out var adjQbr))
                        passingStats["adjqbr"] = adjQbr;
                    break;
                case "qbrating":
                case "rtg":
                    if (double.TryParse(value, out var rating))
                        passingStats["qbrating"] = rating;
                    break;
            }
        }

        if (passingStats.Count == 0)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(passingStats);
    }

    private static object? ConvertToRushingJson(PlayerStatCategory statCategory, PlayerStats playerStatsData)
    {
        var rushingStats = new Dictionary<string, object>();

        for (int i = 0; i < statCategory.Keys.Count && i < playerStatsData.Stats.Count; i++)
        {
            var key = statCategory.Keys[i].ToLower();
            var value = playerStatsData.Stats[i];

            switch (key)
            {
                case "rushingattempts":
                case "car":
                case "carries":
                    if (int.TryParse(value, out var carries))
                        rushingStats["rushingattempts"] = carries;
                    break;
                case "rushingyards":
                case "yds":
                    if (int.TryParse(value, out var yards))
                        rushingStats["rushingyards"] = yards;
                    break;
                case "yardsperrushattempt":
                case "avg":
                    if (double.TryParse(value, out var avg))
                        rushingStats["yardsperrushattempt"] = avg;
                    break;
                case "rushingtouchdowns":
                case "td":
                    if (int.TryParse(value, out var tds))
                        rushingStats["rushingtouchdowns"] = tds;
                    break;
                case "longrushing":
                case "long":
                case "lng":
                    if (int.TryParse(value, out var longest))
                        rushingStats["longrushing"] = longest;
                    break;
                case "rushingfirstdowns":
                    if (int.TryParse(value, out var firstDowns))
                        rushingStats["rushingfirstdowns"] = firstDowns;
                    break;
            }
        }

        if (rushingStats.Count == 0)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(rushingStats);
    }

    private static object? ConvertToReceivingJson(PlayerStatCategory statCategory, PlayerStats playerStatsData)
    {
        var receivingStats = new Dictionary<string, object>();

        for (int i = 0; i < statCategory.Keys.Count && i < playerStatsData.Stats.Count; i++)
        {
            var key = statCategory.Keys[i].ToLower();
            var value = playerStatsData.Stats[i];

            switch (key)
            {
                case "receptions":
                case "rec":
                    if (int.TryParse(value, out var receptions))
                        receivingStats["receptions"] = receptions;
                    break;
                case "receivingyards":
                case "yds":
                    if (int.TryParse(value, out var yards))
                        receivingStats["receivingyards"] = yards;
                    break;
                case "yardsperreception":
                case "avg":
                    if (double.TryParse(value, out var avg))
                        receivingStats["yardsperreception"] = avg;
                    break;
                case "receivingtouchdowns":
                case "td":
                    if (int.TryParse(value, out var tds))
                        receivingStats["receivingtouchdowns"] = tds;
                    break;
                case "longreception":
                case "long":
                case "lng":
                    if (int.TryParse(value, out var longest))
                        receivingStats["longreception"] = longest;
                    break;
                case "receivingtargets":
                case "targ":
                case "targets":
                    if (int.TryParse(value, out var targets))
                        receivingStats["receivingtargets"] = targets;
                    break;
                case "receivingfirstdowns":
                    if (int.TryParse(value, out var firstDowns))
                        receivingStats["receivingfirstdowns"] = firstDowns;
                    break;
            }
        }

        if (receivingStats.Count == 0)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(receivingStats);
    }

    private static void ExtractFumbleStats(PlayerStatCategory statCategory, PlayerStats playerStatsData, ParsedPlayerStat parsedStat)
    {
        for (int i = 0; i < statCategory.Keys.Count && i < playerStatsData.Stats.Count; i++)
        {
            var key = statCategory.Keys[i].ToLower();
            var value = playerStatsData.Stats[i];

            switch (key)
            {
                case "fum":
                case "fumbles":
                    if (int.TryParse(value, out var fumbles))
                        parsedStat.Fumbles = fumbles;
                    break;
                case "lost":
                case "fumbleslost":
                    if (int.TryParse(value, out var lost))
                        parsedStat.FumblesLost = lost;
                    break;
            }
        }
    }
}
