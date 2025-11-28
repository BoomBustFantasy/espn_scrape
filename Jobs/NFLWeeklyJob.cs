using System.Text.Json;
using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ESPNScrape.Jobs;

[DisallowConcurrentExecution]
public class NFLWeeklyJob : IJob
{
    private readonly ILogger<NFLWeeklyJob> _logger;
    private readonly ESPNDataService _espnDataService;
    private readonly ESPNPlayerMappingService _playerMappingService;
    private readonly SupabaseService _supabaseService;

    public NFLWeeklyJob(ILogger<NFLWeeklyJob> logger, ESPNDataService espnDataService, ESPNPlayerMappingService playerMappingService, SupabaseService supabaseService)
    {
        _logger = logger;
        _espnDataService = espnDataService;
        _playerMappingService = playerMappingService;
        _supabaseService = supabaseService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting NFL Weekly games scraping job");

        // Track overall job statistics
        var totalGamesProcessed = 0;
        var totalPlayersFound = 0;
        var totalPlayersMatched = 0;
        var totalRecordsProcessed = 0;

        try
        {
            // Check for job data parameters (used for specific season/week ranges)
            var jobDataMap = context.MergedJobDataMap;
            int currentSeason;
            List<int> weeksToCheck;

            if (jobDataMap.ContainsKey("season"))
            {
                // Use explicit season and week range from job data
                currentSeason = jobDataMap.GetInt("season");
                var startWeek = jobDataMap.ContainsKey("startWeek") ? jobDataMap.GetInt("startWeek") : 1;
                var endWeek = jobDataMap.ContainsKey("endWeek") ? jobDataMap.GetInt("endWeek") : 18;

                weeksToCheck = Enumerable.Range(startWeek, endWeek - startWeek + 1).ToList();

                _logger.LogInformation("Using explicit season {Season}, weeks {StartWeek}-{EndWeek} from job data",
                    currentSeason, startWeek, endWeek);
            }
            else
            {
                // Determine current NFL season and weeks to check
                currentSeason = await GetCurrentNFLSeason();
                weeksToCheck = await GetWeeksToCheck(currentSeason);

                _logger.LogInformation("Determined current season: {Season}, checking weeks: [{Weeks}]",
                    currentSeason, string.Join(", ", weeksToCheck));
            }

            // Process games from all relevant weeks
            foreach (var week in weeksToCheck)
            {
                _logger.LogInformation("=== PROCESSING WEEK {Week} ===", week);

                // Get games for this week
                var games = await _espnDataService.GetNFLWeekGamesAsync(currentSeason, week);

                if (games == null || !games.Any())
                {
                    _logger.LogInformation("No games found for NFL {Season} Week {Week}", currentSeason, week);
                    continue;
                }

                _logger.LogInformation("Found {GameCount} games for NFL {Season} Week {Week}",
                    games.Count(), currentSeason, week);

                // Process each game in this week
                foreach (var game in games)
                {
                    var (gamePlayersFound, gamePlayersMatched, gameRecordsProcessed) = await LogGameMatchup(game, currentSeason, week);
                    totalGamesProcessed++;
                    totalPlayersFound += gamePlayersFound;
                    totalPlayersMatched += gamePlayersMatched;
                    totalRecordsProcessed += gameRecordsProcessed;
                }

                _logger.LogInformation("=== COMPLETED WEEK {Week} ===", week);

                // Add delay between weeks to be respectful to ESPN's servers
                await Task.Delay(500);
            }

            // Log overall job summary
            _logger.LogInformation("🏆 NFL WEEKLY JOB SUMMARY: {TotalGames} games processed | {TotalPlayersFound} players found | {TotalPlayersMatched} matched to DB | {TotalRecordsProcessed} records processed",
                totalGamesProcessed, totalPlayersFound, totalPlayersMatched, totalRecordsProcessed);
            _logger.LogInformation("Completed NFL Weekly games scraping job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while scraping NFL Weekly games");
            throw;
        }
    }

    private async Task<int> GetCurrentNFLSeason()
    {
        try
        {
            // For now, use current year but adjust for NFL season timing
            var currentDate = DateTime.Now;
            var currentYear = currentDate.Year;

            // NFL season typically runs from September to February of next year
            // If we're in January-July, the NFL season year is the previous year
            if (currentDate.Month <= 7)
            {
                return currentYear - 1;
            }

            return currentYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining current NFL season, defaulting to 2025");
            return 2025;
        }
    }

    private async Task<int> GetLastCompletedWeek(int season)
    {
        try
        {
            // Simple approach: based on current date, estimate the current NFL week
            var currentDate = DateTime.Now;

            // NFL season typically starts first week of September
            // For 2025, let's estimate based on current date
            if (currentDate.Month == 10 && currentDate.Day >= 15) // Mid-October
            {
                // We're likely in week 6-8 range, so return a recent week that should have data
                _logger.LogInformation("Based on current date ({CurrentDate}), estimated to be around week 7-8",
                    currentDate.ToString("yyyy-MM-dd"));
                return 7; // Current week as of October 20, 2025
            }

            // Check weeks from most recent backwards to find one with games
            var estimatedWeek = EstimateCurrentWeekFromDate(currentDate);

            for (int week = estimatedWeek; week >= 1; week--)
            {
                var games = await _espnDataService.GetNFLWeekGamesAsync(season, week);

                if (games != null && games.Any())
                {
                    _logger.LogInformation("Found {GameCount} games in {Season} week {Week}",
                        games.Count(), season, week);
                    return week;
                }

                // Add small delay to avoid rate limiting
                await Task.Delay(100);
            }

            // Fallback to week 7 if we can't determine
            _logger.LogWarning("Could not determine last completed week, defaulting to week 7");
            return 7;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining last completed week, defaulting to week 7");
            return 7;
        }
    }

    private int EstimateCurrentWeekFromDate(DateTime date)
    {
        // NFL season typically starts first Thursday after Labor Day (early September)
        // Rough estimation: if it's October 20th, we're probably around week 7-8
        if (date.Month == 9)
        {
            return Math.Max(1, (date.Day - 5) / 7); // Weeks 1-4 in September
        }
        else if (date.Month == 10)
        {
            // Late October 2025 - we're likely in week 8 or 9
            if (date.Day >= 26) return 9;  // Late October likely week 9
            if (date.Day >= 19) return 8;  // Mid-late October likely week 8  
            return Math.Min(8, 4 + (date.Day / 7)); // Earlier October weeks 5-7
        }
        else if (date.Month == 11)
        {
            return Math.Min(12, 8 + (date.Day / 7)); // Weeks 9-12 in November
        }
        else if (date.Month == 12)
        {
            return Math.Min(17, 12 + (date.Day / 7)); // Weeks 13-17 in December
        }
        else if (date.Month == 1)
        {
            return 18; // Week 18 typically in January
        }

        return 7; // Default fallback
    }

    private async Task<List<int>> GetWeeksToCheck(int season)
    {
        try
        {
            var currentDate = DateTime.Now;
            var estimatedCurrentWeek = EstimateCurrentWeekFromDate(currentDate);
            var weeksToCheck = new List<int>();

            _logger.LogInformation("Estimated current week based on date ({CurrentDate}): {EstimatedWeek}",
                currentDate.ToString("yyyy-MM-dd"), estimatedCurrentWeek);

            // Check the estimated current week and the previous week
            // This ensures we catch:
            // 1. Games from last week that might have been missed
            // 2. Games from current week that have already finished
            var startWeek = Math.Max(1, estimatedCurrentWeek - 1); // Previous week
            var endWeek = Math.Min(18, estimatedCurrentWeek); // Current week

            for (int week = startWeek; week <= endWeek; week++)
            {
                // Check if this week has any games
                var games = await _espnDataService.GetNFLWeekGamesAsync(season, week);

                if (games != null && games.Any())
                {
                    weeksToCheck.Add(week);
                    _logger.LogInformation("Week {Week} has {GameCount} games - added to processing list",
                        week, games.Count());
                }
                else
                {
                    _logger.LogInformation("Week {Week} has no games - skipping", week);
                }

                // Add small delay to avoid rate limiting
                await Task.Delay(100);
            }

            // If no weeks found, fallback to estimated current week
            if (!weeksToCheck.Any())
            {
                _logger.LogWarning("No weeks with games found, falling back to estimated week {Week}", estimatedCurrentWeek);
                weeksToCheck.Add(estimatedCurrentWeek);
            }

            return weeksToCheck.OrderBy(w => w).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining weeks to check, falling back to current week estimation");
            var fallbackWeek = EstimateCurrentWeekFromDate(DateTime.Now);
            return new List<int> { fallbackWeek };
        }
    }

    private async Task<(int playersFound, int playersMatched, int recordsProcessed)> LogGameMatchup(Game game, int season, int week)
    {
        try
        {
            if (game.Competitions?.Any() != true)
            {
                _logger.LogWarning("Game {GameId} has no competitions", game.Id);
                return (0, 0, 0);
            }

            var competition = game.Competitions.First();

            if (competition.Competitors?.Count != 2)
            {
                _logger.LogWarning("Game {GameId} does not have exactly 2 competitors", game.Id);
                return (0, 0, 0);
            }

            var homeTeam = competition.Competitors.FirstOrDefault(c => c.HomeAway?.ToLower() == "home");
            var awayTeam = competition.Competitors.FirstOrDefault(c => c.HomeAway?.ToLower() == "away");

            if (homeTeam?.Team == null || awayTeam?.Team == null)
            {
                _logger.LogWarning("Could not find team references for game {GameId}", game.Id);
                return (0, 0, 0);
            }

            // Teams are references, fetch them
            var homeTeamData = await GetTeamFromReference(homeTeam.Team);
            var awayTeamData = await GetTeamFromReference(awayTeam.Team);

            if (homeTeamData != null && awayTeamData != null)
            {
                _logger.LogInformation("NFL Game: {AwayTeam} @ {HomeTeam} - {GameDate}",
                    awayTeamData.DisplayName,
                    homeTeamData.DisplayName,
                    game.Date.ToString("MMM dd, yyyy h:mm tt"));

                // Log winner if available
                if (homeTeam.Winner)
                {
                    _logger.LogInformation("Winner: {Winner}", homeTeamData.DisplayName);
                }
                else if (awayTeam.Winner)
                {
                    _logger.LogInformation("Winner: {Winner}", awayTeamData.DisplayName);
                }

                // Fetch and log offensive player statistics
                return await LogOffensivePlayerStats(game.Id, homeTeamData.DisplayName, awayTeamData.DisplayName, season, week, game.Date);
            }
            else
            {
                _logger.LogWarning("Could not resolve team data for game {GameId}", game.Id);
                return (0, 0, 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging matchup for game {GameId}", game.Id);
            return (0, 0, 0);
        }
    }

    private async Task<(int playersFound, int playersMatched, int recordsProcessed)> LogOffensivePlayerStats(string gameId, string homeTeamName, string awayTeamName, int season, int week, DateTime gameDate)
    {
        try
        {
            // Track game-level statistics
            var totalPlayersFound = 0;
            var totalPlayersMatched = 0;
            var totalRecordsProcessed = 0;

            // Get game summary using existing ESPNDataService
            var gameSummary = await _espnDataService.GetGameSummaryAsync(gameId);

            if (gameSummary?.BoxScore?.Players != null && gameSummary.BoxScore.Players.Count > 0)
            {
                foreach (var teamPlayerStats in gameSummary.BoxScore.Players)
                {
                    var teamName = teamPlayerStats.Team?.DisplayName ?? "Unknown Team";

                    if (teamPlayerStats.Statistics != null && teamPlayerStats.Statistics.Count > 0)
                    {
                        // Filter for the stat categories we want
                        var desiredCategories = new[] { "passing", "rushing", "receiving", "interceptions", "fumbles" };
                        var relevantStats = teamPlayerStats.Statistics
                            .Where(stat => desiredCategories.Contains(stat.Name.ToLower()))
                            .ToList();

                        // Collect all player stats by player ID to combine multiple categories
                        var playerStatsByPlayerId = new Dictionary<string, Models.Supa.PlayerStat>();

                        foreach (var statCategory in relevantStats)
                        {
                            if (statCategory.Athletes != null && statCategory.Athletes.Count > 0)
                            {
                                // Process players in this category and combine with existing records
                                var (found, matched, processed) = await ProcessPlayerCategoryStats(statCategory, teamName, gameId, gameDate, season, week, playerStatsByPlayerId);
                                totalPlayersFound += found;
                                totalPlayersMatched += matched;
                            }
                        }

                        // Now upsert all combined player stats for this team
                        if (playerStatsByPlayerId.Any())
                        {
                            try
                            {
                                var playerStatsToUpsert = playerStatsByPlayerId.Values.ToList();
                                var upsertedCount = await _supabaseService.UpsertPlayerStatsBatchAsync(playerStatsToUpsert);
                                totalRecordsProcessed += upsertedCount;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error upserting player stats for team {TeamName}", teamName);
                            }
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("No PlayerStats found in BoxScore for game {GameId}", gameId);
                return (0, 0, 0);
            }

            // Log game summary
            _logger.LogInformation("Game {GameId}: {TotalFound} players found | {TotalMatched} matched | {TotalProcessed} records processed",
                gameId, totalPlayersFound, totalPlayersMatched, totalRecordsProcessed);

            return (totalPlayersFound, totalPlayersMatched, totalRecordsProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging player stats for game {GameId}", gameId);
            return (0, 0, 0);
        }
    }

    private void LogParsedPlayerStats(OffensivePlayerStats stats, string categoryName)
    {
        switch (categoryName)
        {
            case "passing":
                _logger.LogInformation("{Player} ({Pos}): {Comp}/{Att} ({Pct}%), {Yds} yds, {TD} TD, {Int} INT, {Rating} RTG",
                    stats.PlayerName,
                    stats.Position,
                    stats.Completions ?? 0,
                    stats.Attempts ?? 0,
                    stats.CompletionPercentage?.ToString("F1") ?? "0.0",
                    stats.PassingYards ?? 0,
                    stats.PassingTouchdowns ?? 0,
                    stats.Interceptions ?? 0,
                    stats.QBRating?.ToString("F1") ?? "0.0");
                break;

            case "rushing":
                _logger.LogInformation("{Player} ({Pos}): {Car} carries, {Yds} yds ({Avg} avg), {TD} TD, {Long} long",
                    stats.PlayerName,
                    stats.Position,
                    stats.Carries ?? 0,
                    stats.RushingYards ?? 0,
                    stats.YardsPerCarry?.ToString("F1") ?? "0.0",
                    stats.RushingTouchdowns ?? 0,
                    stats.LongestRush ?? 0);
                break;

            case "receiving":
                _logger.LogInformation("{Player} ({Pos}): {Rec} rec, {Yds} yds ({Avg} avg), {TD} TD, {Long} long",
                    stats.PlayerName,
                    stats.Position,
                    stats.Receptions ?? 0,
                    stats.ReceivingYards ?? 0,
                    stats.YardsPerReception?.ToString("F1") ?? "0.0",
                    stats.ReceivingTouchdowns ?? 0,
                    stats.LongestReception ?? 0);
                break;
        }
    }

    private static bool IsOffensiveCategory(string categoryName)
    {
        var offensiveCategories = new[] {
            "passing", "rushing", "receiving",
            "offense", "quarterback", "runningback", "wide receiver", "tight end",
            "qb", "rb", "wr", "te"
        };
        return offensiveCategories.Any(cat => categoryName.ToLower().Contains(cat));
    }

    private async Task<Models.Team?> GetTeamFromReference(TeamReference teamReference)
    {
        try
        {
            if (string.IsNullOrEmpty(teamReference.GetUrl()))
                return null;

            var teamData = await _espnDataService.GetTeamFromUrlAsync(teamReference.GetUrl());
            return teamData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch team from reference: {TeamRef}", teamReference.GetUrl());
            return null;
        }
    }

    private async Task<(int totalFound, int totalMatched, int totalProcessed)> MapAndUpsertPlayersAsync(PlayerStatCategory statCategory, string teamName, string gameId, DateTime gameDate, int season, int week)
    {
        // We'll collect all stats for this category but not upsert yet
        // The calling method will handle combining all categories per player
        var totalFound = statCategory.Athletes?.Count ?? 0;
        var totalMatched = 0;

        if (statCategory.Athletes == null)
            return (0, 0, 0);

        foreach (var playerStats in statCategory.Athletes)
        {
            var espnPlayer = playerStats.Athlete;
            var playerName = espnPlayer?.DisplayName ?? "Unknown Player";
            var espnPlayerId = espnPlayer?.Id ?? "";

            // Map ESPN player to database player
            Models.Supa.Player? mappedPlayer = null;
            if (espnPlayer != null && !string.IsNullOrEmpty(espnPlayerId))
            {
                try
                {
                    mappedPlayer = await _playerMappingService.MapEspnPlayerToSupabaseAsync(espnPlayer, teamName);

                    if (mappedPlayer != null)
                    {
                        totalMatched++;
                        _logger.LogDebug("    ✓ {PlayerName} → DB Player ID: {PlayerId}",
                            playerName, mappedPlayer.Id);
                    }
                    else
                    {
                        _logger.LogWarning("    ✗ Could not map to database player");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "    ✗ Error mapping player {PlayerName} (ESPN ID: {EspnId})",
                        playerName, espnPlayerId);
                }
            }

            // Log player statistics
            if (playerStats.Stats != null && playerStats.Stats.Count > 0)
            {
                var statsWithLabels = statCategory.Labels
                    .Zip(playerStats.Stats, (label, stat) => $"{label}: {stat}")
                    .ToList();

                _logger.LogInformation("    Stats: {Stats}", string.Join(" | ", statsWithLabels));
            }
            else
            {
                _logger.LogInformation("    No stats available");
            }
        }

        return (totalFound, totalMatched, 0); // Will process records at team level
    }

    private async Task<(int totalFound, int totalMatched, int totalProcessed)> ProcessPlayerCategoryStats(
        PlayerStatCategory statCategory, string teamName, string gameId, DateTime gameDate,
        int season, int week, Dictionary<string, Models.Supa.PlayerStat> playerStatsByPlayerId)
    {
        var totalFound = statCategory.Athletes?.Count ?? 0;
        var totalMatched = 0;

        if (statCategory.Athletes == null)
            return (0, 0, 0);

        foreach (var playerStats in statCategory.Athletes)
        {
            var espnPlayer = playerStats.Athlete;
            var playerName = espnPlayer?.DisplayName ?? "Unknown Player";
            var espnPlayerId = espnPlayer?.Id ?? "";

            if (string.IsNullOrEmpty(espnPlayerId))
                continue;

            // Map ESPN player to database player
            Models.Supa.Player? mappedPlayer = null;
            try
            {
                mappedPlayer = await _playerMappingService.MapEspnPlayerToSupabaseAsync(espnPlayer!, teamName);
                if (mappedPlayer != null)
                {
                    totalMatched++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not map player {PlayerName} (ESPN ID: {EspnId}): {Message}",
                    playerName, espnPlayerId, ex.Message);
            }

            // Get or create combined PlayerStat record for this player
            if (!playerStatsByPlayerId.TryGetValue(espnPlayerId, out var existingPlayerStat))
            {
                // Create new record
                var playerCode = $"ESPN_{espnPlayerId}";
                existingPlayerStat = new Models.Supa.PlayerStat
                {
                    PlayerCode = playerCode,
                    Name = playerName,
                    Team = teamName,
                    EspnPlayerId = espnPlayerId,
                    EspnGameId = gameId,
                    PlayerId = mappedPlayer?.Id,
                    Season = season,
                    Week = week,
                    GameDate = gameDate,
                    GameLocation = "",
                    Fumbles = 0,
                    FumblesLost = 0
                };
                playerStatsByPlayerId[espnPlayerId] = existingPlayerStat;
            }

            // Add stats for this category to the existing record
            switch (statCategory.Name.ToLower())
            {
                case "passing":
                    existingPlayerStat.Passing = ConvertToPassingJson(statCategory, playerStats);
                    break;
                case "rushing":
                    existingPlayerStat.Rushing = ConvertToRushingJson(statCategory, playerStats);
                    break;
                case "receiving":
                    existingPlayerStat.Receiving = ConvertToReceivingJson(statCategory, playerStats);
                    break;
                case "fumbles":
                    ExtractFumbleStats(statCategory, playerStats, existingPlayerStat);
                    break;
            }
        }

        return (totalFound, totalMatched, 0);
    }

    private Models.Supa.PlayerStat? CreatePlayerStatRecord(PlayerInfo? espnPlayer, Models.Supa.Player? mappedPlayer,
        PlayerStatCategory statCategory, PlayerStats playerStatsData, string teamName, string gameId, DateTime gameDate, int season, int week)
    {
        try
        {
            // Create a unique player code for this record (using ESPN player ID or name+team as fallback)
            var playerCode = !string.IsNullOrEmpty(espnPlayer?.Id)
                ? $"ESPN_{espnPlayer.Id}"
                : $"{espnPlayer?.DisplayName?.Replace(" ", "_")}_{teamName}".Replace(" ", "_");

            var playerStat = new Models.Supa.PlayerStat
            {
                PlayerCode = playerCode,
                Name = espnPlayer?.DisplayName ?? "Unknown Player",
                Team = teamName,
                EspnPlayerId = espnPlayer?.Id,
                EspnGameId = gameId,
                PlayerId = mappedPlayer?.Id,
                Season = season,
                Week = week,
                GameDate = gameDate,
                GameLocation = "", // We could add this if needed
                Fumbles = 0,
                FumblesLost = 0
            };

            // Convert the stats based on category type
            switch (statCategory.Name.ToLower())
            {
                case "passing":
                    playerStat.Passing = ConvertToPassingJson(statCategory, playerStatsData);
                    break;
                case "rushing":
                    playerStat.Rushing = ConvertToRushingJson(statCategory, playerStatsData);
                    break;
                case "receiving":
                    playerStat.Receiving = ConvertToReceivingJson(statCategory, playerStatsData);
                    break;
                case "fumbles":
                    // Handle fumbles as separate fields
                    ExtractFumbleStats(statCategory, playerStatsData, playerStat);
                    break;
                case "interceptions":
                    // Could be added to passing stats or handled separately
                    break;
            }

            return playerStat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PlayerStat record for {PlayerName}", espnPlayer?.DisplayName);
            return null;
        }
    }

    private object? ConvertToPassingJson(PlayerStatCategory statCategory, PlayerStats playerStatsData)
    {
        try
        {
            var passingStats = new Dictionary<string, object>();

            // Map ESPN stat keys to our JSON structure
            for (int i = 0; i < statCategory.Keys.Count && i < playerStatsData.Stats.Count; i++)
            {
                var key = statCategory.Keys[i].ToLower();
                var value = playerStatsData.Stats[i];

                // Map ESPN keys to our database field names
                switch (key)
                {
                    case "completions/passingattempts":
                    case "completions/attempts":
                        // Parse "18/25" format
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
                        // Handle "2-11" format (sacks-yards lost)
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

            // Return null if no stats were parsed, otherwise return JSON string
            if (passingStats.Count == 0)
                return null;

            // Serialize to JSON string to avoid JsonElement issues
            return System.Text.Json.JsonSerializer.Serialize(passingStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting passing stats to JSON");
            return null;
        }
    }

    private object? ConvertToRushingJson(PlayerStatCategory statCategory, PlayerStats playerStatsData)
    {
        try
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

            // Return null if no stats were parsed, otherwise return JSON string
            if (rushingStats.Count == 0)
                return null;

            // Serialize to JSON string to avoid JsonElement issues
            return System.Text.Json.JsonSerializer.Serialize(rushingStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting rushing stats to JSON");
            return null;
        }
    }

    private object? ConvertToReceivingJson(PlayerStatCategory statCategory, PlayerStats playerStatsData)
    {
        try
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

            // Return null if no stats were parsed, otherwise return JSON string
            if (receivingStats.Count == 0)
                return null;

            // Serialize to JSON string to avoid JsonElement issues
            return System.Text.Json.JsonSerializer.Serialize(receivingStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting receiving stats to JSON");
            return null;
        }
    }

    private void ExtractFumbleStats(PlayerStatCategory statCategory, PlayerStats playerStatsData, Models.Supa.PlayerStat playerStat)
    {
        try
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
                            playerStat.Fumbles = fumbles;
                        break;
                    case "lost":
                    case "fumbleslost":
                        if (int.TryParse(value, out var lost))
                            playerStat.FumblesLost = lost;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fumble stats");
        }
    }
}