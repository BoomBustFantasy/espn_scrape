using ESPNScrape.Models;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job that syncs NFL player data from ESPN API to the Players table
/// Focuses on filling in missing ESPN player IDs for existing players
/// </summary>
[DisallowConcurrentExecution]
public class NFLPlayerSyncJob : IJob
{
    private readonly ILogger<NFLPlayerSyncJob> _logger;
    private readonly IESPNDataService _espnDataService;
    private readonly ISupabaseService _supabaseService;
    private readonly IESPNPlayerMappingService _playerMappingService;

    public NFLPlayerSyncJob(
        ILogger<NFLPlayerSyncJob> logger,
        IESPNDataService espnDataService,
        ISupabaseService supabaseService,
        IESPNPlayerMappingService playerMappingService)
    {
        _logger = logger;
        _espnDataService = espnDataService;
        _supabaseService = supabaseService;
        _playerMappingService = playerMappingService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("🏈 Starting NFL Player Sync job - syncing ESPN player IDs");

        // Track overall job statistics
        var totalPlayersProcessed = 0;
        var totalPlayersMatched = 0;
        var totalPlayersUpdated = 0;
        var totalNewPlayers = 0;
        var totalErrors = 0;

        try
        {
            // Get current NFL season
            var currentSeason = await GetCurrentNFLSeason();
            _logger.LogInformation("Processing players for NFL {Season} season", currentSeason);

            // Get all NFL teams for the current season from ESPN
            var espnTeams = await _espnDataService.GetNFLTeamsAsync(currentSeason);

            if (espnTeams == null || !espnTeams.Any())
            {
                _logger.LogWarning("No NFL teams found for season {Season}", currentSeason);
                return;
            }

            _logger.LogInformation("Found {TeamCount} NFL teams for season {Season}", espnTeams.Count, currentSeason);

            // Process each team's roster
            foreach (var espnTeam in espnTeams)
            {
                _logger.LogInformation("=== PROCESSING TEAM: {TeamName} (ESPN ID: {EspnTeamId}) ===",
                    espnTeam.DisplayName, espnTeam.Id);

                try
                {
                    // Get Supabase team ID for this ESPN team
                    var supabaseTeamId = ESPNTeamMapper.MapEspnIdToSupabaseId(espnTeam.Id);
                    if (!supabaseTeamId.HasValue)
                    {
                        _logger.LogWarning("⚠️ No Supabase team mapping found for ESPN team {TeamName} (ID: {EspnTeamId})",
                            espnTeam.DisplayName, espnTeam.Id);
                        continue;
                    }

                    // Get team roster from ESPN
                    var espnRoster = await _espnDataService.GetTeamRosterAsync(currentSeason, espnTeam.Id);

                    if (espnRoster == null || !espnRoster.Any())
                    {
                        _logger.LogInformation("No roster found for {TeamName}", espnTeam.DisplayName);
                        continue;
                    }

                    _logger.LogInformation("Found {PlayerCount} players on {TeamName} roster",
                        espnRoster.Count, espnTeam.DisplayName);

                    // Get all players from this team in our database
                    var dbPlayers = await GetTeamPlayersFromDatabase(supabaseTeamId.Value);
                    _logger.LogInformation("Found {DbPlayerCount} players in database for team {TeamName}",
                        dbPlayers.Count, espnTeam.DisplayName);

                    // Process each player on the ESPN roster
                    foreach (var espnPlayer in espnRoster)
                    {
                        totalPlayersProcessed++;

                        try
                        {
                            var result = await ProcessPlayerSync(espnPlayer, espnTeam.DisplayName, supabaseTeamId.Value, dbPlayers);

                            if (result.Matched) totalPlayersMatched++;
                            if (result.Updated) totalPlayersUpdated++;
                            if (result.NewPlayer) totalNewPlayers++;
                        }
                        catch (Exception ex)
                        {
                            totalErrors++;
                            _logger.LogError(ex, "Error processing player {PlayerName} (ESPN ID: {EspnId})",
                                espnPlayer.DisplayName, espnPlayer.Id);
                        }

                        // Rate limiting - be respectful to ESPN's API
                        await Task.Delay(100);
                    }

                    _logger.LogInformation("=== COMPLETED TEAM: {TeamName} ===", espnTeam.DisplayName);
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    _logger.LogError(ex, "Error processing team {TeamName}", espnTeam.DisplayName);
                }

                // Delay between teams
                await Task.Delay(1000);
            }

            // Log overall job summary
            _logger.LogInformation(
                "🏈 NFL PLAYER SYNC JOB SUMMARY: {TotalPlayers} players processed | {TotalMatched} matched | {TotalUpdated} updated | {TotalNew} new | {TotalErrors} errors",
                totalPlayersProcessed, totalPlayersMatched, totalPlayersUpdated, totalNewPlayers, totalErrors);
            _logger.LogInformation("✅ Completed NFL Player Sync job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Fatal error occurred while syncing NFL players");
            throw;
        }
    }

    private async Task<(bool Matched, bool Updated, bool NewPlayer)> ProcessPlayerSync(
        Player espnPlayer,
        string teamName,
        int supabaseTeamId,
        List<Models.Supa.Player> dbPlayers)
    {
        var playerName = espnPlayer.DisplayName ?? "Unknown Player";
        var espnPlayerId = espnPlayer.Id ?? "";

        if (string.IsNullOrEmpty(espnPlayerId))
        {
            _logger.LogWarning("⚠️ Player {PlayerName} has no ESPN ID, skipping", playerName);
            return (false, false, false);
        }

        // Check if we already have this player by ESPN ID
        var existingPlayerByEspnId = await _supabaseService.GetPlayerByEspnIdAsync(espnPlayerId);
        if (existingPlayerByEspnId != null)
        {
            _logger.LogDebug("✅ Player {PlayerName} already has ESPN ID {EspnId}",
                playerName, espnPlayerId);
            return (true, false, false);
        }

        // Try to find player by name in the database players for this team
        var matchedPlayer = FindPlayerByName(espnPlayer, dbPlayers);

        if (matchedPlayer != null)
        {
            // Found a match! Update with ESPN player ID
            _logger.LogInformation("🎯 Matched ESPN player {EspnName} (ID: {EspnId}) to database player {DbName} (ID: {DbId})",
                playerName, espnPlayerId,
                $"{matchedPlayer.FirstName} {matchedPlayer.LastName}", matchedPlayer.Id);

            var updateSuccess = await _supabaseService.UpdatePlayerEspnIdAsync(matchedPlayer.Id, espnPlayerId);

            if (updateSuccess)
            {
                _logger.LogInformation("✅ Successfully updated player {PlayerName} with ESPN ID {EspnId}",
                    $"{matchedPlayer.FirstName} {matchedPlayer.LastName}", espnPlayerId);
                return (true, true, false);
            }
            else
            {
                _logger.LogError("❌ Failed to update player {PlayerName} with ESPN ID {EspnId}",
                    $"{matchedPlayer.FirstName} {matchedPlayer.LastName}", espnPlayerId);
                return (true, false, false);
            }
        }
        else
        {
            // No match found in database
            _logger.LogDebug("ℹ️ No database match found for ESPN player {PlayerName} (ID: {EspnId}) on team {TeamName}",
                playerName, espnPlayerId, teamName);
            return (false, false, false);
        }
    }

    private Models.Supa.Player? FindPlayerByName(Player espnPlayer, List<Models.Supa.Player> dbPlayers)
    {
        if (espnPlayer == null || string.IsNullOrEmpty(espnPlayer.FirstName) || string.IsNullOrEmpty(espnPlayer.LastName))
            return null;

        var espnFirstName = NormalizeName(espnPlayer.FirstName);
        var espnLastName = NormalizeName(espnPlayer.LastName);

        // Try exact match first
        var exactMatch = dbPlayers.FirstOrDefault(p =>
            NormalizeName(p.FirstName) == espnFirstName &&
            NormalizeName(p.LastName) == espnLastName);

        if (exactMatch != null)
        {
            _logger.LogDebug("Found exact match: '{DbFirst}' '{DbLast}' (ID: {PlayerId})",
                exactMatch.FirstName, exactMatch.LastName, exactMatch.Id);
            return exactMatch;
        }

        // Try fuzzy matching for common name variations
        var fuzzyMatch = dbPlayers.FirstOrDefault(p =>
        {
            var dbFirstName = NormalizeName(p.FirstName);
            var dbLastName = NormalizeName(p.LastName);

            // Check if last names match exactly and first names are similar
            if (dbLastName == espnLastName)
            {
                // Handle common first name variations
                // e.g., "Pat" vs "Patrick", "Mike" vs "Michael"
                if (dbFirstName.StartsWith(espnFirstName) || espnFirstName.StartsWith(dbFirstName))
                    return true;

                // Handle middle names/initials
                var dbFirstParts = dbFirstName.Split(' ');
                var espnFirstParts = espnFirstName.Split(' ');

                if (dbFirstParts.Length > 0 && espnFirstParts.Length > 0)
                {
                    if (dbFirstParts[0] == espnFirstParts[0])
                        return true;
                }
            }

            return false;
        });

        if (fuzzyMatch != null)
        {
            _logger.LogDebug("Fuzzy matched ESPN '{EspnFirst} {EspnLast}' to DB '{DbFirst} {DbLast}'",
                espnPlayer.FirstName, espnPlayer.LastName,
                fuzzyMatch.FirstName, fuzzyMatch.LastName);
        }

        return fuzzyMatch;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        // Convert to uppercase, remove extra spaces, trim
        // Remove periods (for names like "St. Brown" vs "St Brown")
        // Remove common suffixes like Jr., Sr., III, etc.
        var normalized = name.Trim().ToUpperInvariant();
        normalized = normalized.Replace(".", ""); // Remove periods
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+(JR\.?|SR\.?|III|IV|II)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return normalized.Trim();
    }

    private async Task<List<Models.Supa.Player>> GetTeamPlayersFromDatabase(int teamId)
    {
        try
        {
            // Query directly for this team's players instead of fetching all players
            // This is more efficient and avoids pagination issues
            var teamPlayers = await _supabaseService.GetPlayersByTeamIdAsync(teamId);

            return teamPlayers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting players from database for team {TeamId}", teamId);
            return new List<Models.Supa.Player>();
        }
    }

    private async Task<int> GetCurrentNFLSeason()
    {
        try
        {
            var currentDate = DateTime.Now;
            var currentYear = currentDate.Year;

            // NFL season runs from September to February of next year
            // If we're in January-July, the NFL season year is the previous year
            if (currentDate.Month <= 7)
            {
                return currentYear - 1;
            }

            return currentYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining current NFL season, defaulting to 2024");
            return 2024;
        }
    }
}
