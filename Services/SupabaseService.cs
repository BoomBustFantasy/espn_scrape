using ESPNScrape.Configuration;
using ESPNScrape.Models.Supa;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Interfaces;

namespace ESPNScrape.Services;

/// <summary>
/// Service for interacting with Supabase database
/// Handles player lookups and player stats upserts for ESPN data integration
/// </summary>
public class SupabaseService : ISupabaseService
{
    private readonly Client _supabaseClient;
    private readonly ILogger<SupabaseService> _logger;

    public SupabaseService(IOptions<SupabaseSettings> settings, ILogger<SupabaseService> logger)
    {
        _logger = logger;
        var config = settings.Value;

        if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.ServiceRoleKey))
        {
            throw new InvalidOperationException("Supabase configuration is missing");
        }

        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false, // We don't need realtime for batch operations
            AutoRefreshToken = false     // We're using service operations, not user authentication
        };

        _supabaseClient = new Client(config.Url, config.ServiceRoleKey, options);

        // Initialize the client
        Task.Run(async () => await _supabaseClient.InitializeAsync());
    }

    /// <summary>
    /// Gets all players from the database, optionally filtered by ESPN player ID
    /// Used for matching ESPN players to existing database records
    /// </summary>
    /// <param name="espnPlayerId">Optional ESPN player ID to filter by</param>
    /// <returns>List of players, or single player if ESPN ID specified</returns>
    public async Task<List<Player>> GetPlayersAsync(string? espnPlayerId = null)
    {
        try
        {
            var query = _supabaseClient
                .From<Player>()
                .Select("*");

            if (!string.IsNullOrEmpty(espnPlayerId))
            {
                query = query.Where(p => p.EspnPlayerId == espnPlayerId);
            }

            var response = await query.Get();
            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving players from database. ESPN Player ID: {EspnPlayerId}", espnPlayerId);
            return new List<Player>();
        }
    }

    /// <summary>
    /// Gets all players for a specific team
    /// More efficient than fetching all players and filtering
    /// </summary>
    /// <param name="teamId">Team ID to filter by</param>
    /// <returns>List of players for the specified team</returns>
    public async Task<List<Player>> GetPlayersByTeamIdAsync(int teamId)
    {
        try
        {
            var response = await _supabaseClient
                .From<Player>()
                .Select("*")
                .Where(p => p.TeamId == teamId)
                .Get();

            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving players for team {TeamId}", teamId);
            return new List<Player>();
        }
    }

    /// <summary>
    /// Finds a player by their ESPN player ID
    /// Primary method for matching ESPN data to database records
    /// </summary>
    /// <param name="espnPlayerId">ESPN player identifier</param>
    /// <returns>Player record if found, null otherwise</returns>
    public async Task<Player?> GetPlayerByEspnIdAsync(string espnPlayerId)
    {
        if (string.IsNullOrEmpty(espnPlayerId))
        {
            _logger.LogWarning("GetPlayerByEspnIdAsync called with empty ESPN player ID");
            return null;
        }

        try
        {
            var players = await GetPlayersAsync(espnPlayerId);
            var player = players.FirstOrDefault();

            if (player == null)
            {
                _logger.LogDebug("No player found with ESPN ID: {EspnPlayerId}", espnPlayerId);
            }

            return player;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding player by ESPN ID: {EspnPlayerId}", espnPlayerId);
            return null;
        }
    }

    /// <summary>
    /// Searches for players by name and team for fuzzy matching
    /// Used when ESPN player ID is not available or doesn't match
    /// </summary>
    /// <param name="firstName">Player's first name</param>
    /// <param name="lastName">Player's last name</param>
    /// <param name="teamAbbreviation">Team abbreviation (optional)</param>
    /// <returns>List of potential player matches</returns>
    public async Task<List<Player>> SearchPlayersByNameAsync(string firstName, string lastName, string? teamAbbreviation = null)
    {
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            _logger.LogWarning("SearchPlayersByNameAsync called with empty name parameters");
            return new List<Player>();
        }

        try
        {
            var query = _supabaseClient
                .From<Player>()
                .Select("*");

            var response = await query.Get();
            var allPlayers = response.Models;

            // Filter by name
            var filteredPlayers = allPlayers
                .Where(p => p.FirstName.Contains(firstName, StringComparison.OrdinalIgnoreCase) &&
                           p.LastName.Contains(lastName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Add team filter if provided (client-side filtering for now)
            if (!string.IsNullOrEmpty(teamAbbreviation))
            {
                // This will need team data loaded separately or joined properly
                return filteredPlayers;
            }

            return filteredPlayers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for players by name: {FirstName} {LastName}, Team: {Team}",
                firstName, lastName, teamAbbreviation);
            return new List<Player>();
        }
    }

    /// <summary>
    /// Upserts player statistics into the PlayerStats table
    /// Uses composite primary key (player_code, game_date) for conflict resolution
    /// </summary>
    /// <param name="playerStat">Player statistics to insert or update</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> UpsertPlayerStatAsync(PlayerStat playerStat)
    {
        if (playerStat == null)
        {
            _logger.LogWarning("UpsertPlayerStatAsync called with null PlayerStat");
            return false;
        }

        if (string.IsNullOrEmpty(playerStat.PlayerCode))
        {
            _logger.LogWarning("UpsertPlayerStatAsync called with empty PlayerCode");
            return false;
        }

        try
        {
            // Set timestamps
            var now = DateTime.UtcNow;
            if (playerStat.CreatedAt == default)
                playerStat.CreatedAt = now;
            playerStat.UpdatedAt = now;

            // Use built-in Upsert with OnConflict to specify composite key fields
            // Don't set ID - let database handle auto-increment
            // Remove explicit ID assignment to allow auto-increment

            var result = await _supabaseClient
                .From<PlayerStat>()
                .OnConflict(x => new { x.PlayerCode, x.GameDate }) // Specify composite key for conflict resolution
                .Upsert(playerStat);

            _logger.LogDebug("Successfully upserted player stat for {PlayerCode} on {GameDate}",
                playerStat.PlayerCode, playerStat.GameDate.ToString("yyyy-MM-dd"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting player stat for {PlayerCode} on {GameDate}",
                playerStat.PlayerCode, playerStat.GameDate.ToString("yyyy-MM-dd"));
            return false;
        }
    }

    /// <summary>
    /// Batch upserts multiple player statistics for better performance
    /// Recommended for processing multiple games or players at once
    /// </summary>
    /// <param name="playerStats">Collection of player statistics to upsert</param>
    /// <returns>Number of successfully processed records</returns>
    public async Task<int> UpsertPlayerStatsBatchAsync(IEnumerable<PlayerStat> playerStats)
    {
        if (playerStats == null || !playerStats.Any())
        {
            _logger.LogWarning("UpsertPlayerStatsBatchAsync called with empty collection");
            return 0;
        }

        var statsList = playerStats.ToList();
        var now = DateTime.UtcNow;

        // Track statistics
        var totalRecords = statsList.Count;
        var updatedCount = 0;
        var insertedCount = 0;
        var failedCount = 0;

        // Set timestamps for all records
        foreach (var stat in statsList)
        {
            if (stat.CreatedAt == default)
                stat.CreatedAt = now;
            stat.UpdatedAt = now;
        }

        try
        {
            // Process each record individually since batch upsert doesn't work with our unique constraint
            foreach (var stat in statsList)
            {
                try
                {
                    // Try to find existing record first
                    var existing = await _supabaseClient
                        .From<PlayerStat>()
                        .Where(x => x.EspnPlayerId == stat.EspnPlayerId && x.EspnGameId == stat.EspnGameId)
                        .Get();

                    if (existing.Models.Any())
                    {
                        // Update existing record
                        var existingRecord = existing.Models.First();
                        existingRecord.Passing = stat.Passing;
                        existingRecord.Rushing = stat.Rushing;
                        existingRecord.Receiving = stat.Receiving;
                        existingRecord.Fumbles = stat.Fumbles;
                        existingRecord.FumblesLost = stat.FumblesLost;
                        existingRecord.UpdatedAt = DateTime.UtcNow;

                        await existingRecord.Update<PlayerStat>();
                        updatedCount++;
                    }
                    else
                    {
                        // Insert new record - ensure ID is not set to let database auto-increment
                        stat.Id = null;
                        var result = await _supabaseClient
                            .From<PlayerStat>()
                            .Insert(stat);
                        insertedCount++;
                    }
                }
                catch (Supabase.Postgrest.Exceptions.PostgrestException pgEx) when (pgEx.Message.Contains("23503"))
                {
                    // Foreign key constraint violation - log but continue processing
                    failedCount++;
                    _logger.LogWarning("⚠️ Skipped record due to foreign key constraint: ESPN Player ID {EspnPlayerId} not found in Players table. Game: {EspnGameId}",
                        stat.EspnPlayerId, stat.EspnGameId);
                }
                catch (Exception individualEx)
                {
                    failedCount++;
                    _logger.LogError(individualEx, "Failed to process record for ESPN Player ID: {EspnPlayerId}, Game ID: {EspnGameId}",
                        stat.EspnPlayerId, stat.EspnGameId);
                }

                // Small delay between operations to be respectful to the API
                await Task.Delay(50);
            }

            _logger.LogInformation("📊 Database Upsert Summary: {TotalRecords} total records | {UpdatedCount} updated | {InsertedCount} inserted | {FailedCount} failed",
                totalRecords, updatedCount, insertedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch upsert of player stats");
        }

        return updatedCount + insertedCount;
    }

    /// <summary>
    /// Gets player statistics for a specific player and date range
    /// Useful for checking existing data before processing
    /// </summary>
    /// <param name="playerCode">Player code (composite PK component)</param>
    /// <param name="startDate">Start date for range (optional)</param>
    /// <param name="endDate">End date for range (optional)</param>
    /// <returns>List of player statistics</returns>
    public async Task<List<PlayerStat>> GetPlayerStatsAsync(string playerCode, DateTime? startDate = null, DateTime? endDate = null)
    {
        if (string.IsNullOrEmpty(playerCode))
        {
            _logger.LogWarning("GetPlayerStatsAsync called with empty player code");
            return new List<PlayerStat>();
        }

        try
        {
            var query = _supabaseClient
                .From<PlayerStat>()
                .Select("*")
                .Where(ps => ps.PlayerCode == playerCode);

            if (startDate.HasValue)
            {
                query = query.Where(ps => ps.GameDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(ps => ps.GameDate <= endDate.Value);
            }

            var response = await query.Get();
            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving player stats for {PlayerCode}", playerCode);
            return new List<PlayerStat>();
        }
    }

    /// <summary>
    /// Updates a player's ESPN ID if it's missing or incorrect
    /// Used during the matching process to improve future lookups
    /// </summary>
    /// <param name="playerId">Database player ID</param>
    /// <param name="espnPlayerId">ESPN player ID to set</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> UpdatePlayerEspnIdAsync(long playerId, string espnPlayerId)
    {
        if (playerId <= 0 || string.IsNullOrEmpty(espnPlayerId))
        {
            _logger.LogWarning("UpdatePlayerEspnIdAsync called with invalid parameters: PlayerId={PlayerId}, EspnId={EspnId}",
                playerId, espnPlayerId);
            return false;
        }

        try
        {
            // Fetch the existing player first to avoid overwriting other fields with default values
            var existingPlayer = await _supabaseClient
                .From<Player>()
                .Where(p => p.Id == playerId)
                .Single();

            if (existingPlayer == null)
            {
                _logger.LogWarning("Player {PlayerId} not found in database", playerId);
                return false;
            }

            // Update only the ESPN player ID and updated_at timestamp
            existingPlayer.EspnPlayerId = espnPlayerId;
            existingPlayer.UpdatedAt = DateTime.UtcNow;

            await existingPlayer.Update<Player>();

            _logger.LogInformation("Updated ESPN player ID for player {PlayerId} to {EspnPlayerId}",
                playerId, espnPlayerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ESPN player ID for player {PlayerId}", playerId);
            return false;
        }
    }

    /// <summary>
    /// Gets team information by abbreviation for player stat associations
    /// </summary>
    /// <param name="abbreviation">Team abbreviation</param>
    /// <returns>Team record if found, null otherwise</returns>
    public async Task<Team?> GetTeamByAbbreviationAsync(string abbreviation)
    {
        if (string.IsNullOrEmpty(abbreviation))
        {
            _logger.LogWarning("GetTeamByAbbreviationAsync called with empty abbreviation");
            return null;
        }

        try
        {
            var response = await _supabaseClient
                .From<Team>()
                .Select("*")
                .Filter("abbreviation", Supabase.Postgrest.Constants.Operator.Equals, abbreviation.ToUpper())
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team by abbreviation: {Abbreviation}", abbreviation);
            return null;
        }
    }

    /// <summary>
    /// Updates a player record in the database with headshot information
    /// Only updates headshot-related fields to avoid navigation property serialization issues
    /// </summary>
    /// <param name="player">Player record to update</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> UpdatePlayerAsync(Player player)
    {
        if (player == null || player.Id <= 0)
        {
            _logger.LogWarning("UpdatePlayerAsync called with invalid player");
            return false;
        }

        try
        {
            player.UpdatedAt = DateTime.UtcNow;
            player.HeadshotUpdatedAt = DateTime.UtcNow;

            await _supabaseClient
                .From<Player>()
                .Where(p => p.Id == player.Id)
                .Update(player);

            _logger.LogDebug("Successfully updated player {PlayerId}", player.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId}", player.Id);
            return false;
        }
    }

    /// <summary>
    /// Uploads an image to Supabase Storage
    /// </summary>
    /// <param name="bucketName">Storage bucket name</param>
    /// <param name="path">Storage path for the file</param>
    /// <param name="imageData">Image data as byte array</param>
    /// <returns>Upload result with success status and public URL</returns>
    public async Task<(bool Success, string? PublicUrl, string? Error)> UploadImageAsync(string bucketName, string path, byte[] imageData)
    {
        if (string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(path) || imageData == null || imageData.Length == 0)
        {
            return (false, null, "Invalid parameters for image upload");
        }

        try
        {
            _logger.LogDebug("Uploading image to bucket {Bucket}, path {Path}, size {Size} bytes",
                bucketName, path, imageData.Length);

            // Upload the file to Supabase Storage
            var uploadResult = await _supabaseClient.Storage
                .From(bucketName)
                .Upload(imageData, path, new Supabase.Storage.FileOptions
                {
                    ContentType = GetContentTypeFromPath(path),
                    Upsert = true // Allow overwriting existing files
                });

            if (!string.IsNullOrEmpty(uploadResult))
            {
                // Get the public URL for the uploaded file
                var publicUrl = _supabaseClient.Storage
                    .From(bucketName)
                    .GetPublicUrl(path);

                _logger.LogDebug("Successfully uploaded image - Public URL: {PublicUrl}", publicUrl);
                return (true, publicUrl, null);
            }
            else
            {
                var error = "Upload failed - no response from storage service";
                _logger.LogError(error);
                return (false, null, error);
            }
        }
        catch (Exception ex)
        {
            var error = $"Error uploading image: {ex.Message}";
            _logger.LogError(ex, "Error uploading image to bucket {Bucket}, path {Path}", bucketName, path);
            return (false, null, error);
        }
    }

    /// <summary>
    /// Determines content type from file path extension
    /// </summary>
    /// <param name="path">File path</param>
    /// <returns>MIME content type</returns>
    private static string GetContentTypeFromPath(string path)
    {
        var extension = Path.GetExtension(path)?.ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg" // Default fallback
        };
    }

    /// <summary>
    /// Gets a schedule record by ESPN game ID
    /// </summary>
    /// <param name="espnGameId">ESPN game ID</param>
    /// <returns>Schedule record if found, null otherwise</returns>
    public async Task<Schedule?> GetScheduleByEspnGameIdAsync(string espnGameId)
    {
        try
        {
            var result = await _supabaseClient
                .From<Schedule>()
                .Where(s => s.EspnGameId == espnGameId)
                .Get();

            return result.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule by ESPN game ID {EspnGameId}", espnGameId);
            return null;
        }
    }

    /// <summary>
    /// Creates a new schedule record
    /// </summary>
    /// <param name="schedule">Schedule record to create</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> CreateScheduleAsync(Schedule schedule)
    {
        try
        {
            schedule.CreatedAt = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;

            await _supabaseClient
                .From<Schedule>()
                .Insert(schedule);

            _logger.LogDebug("Successfully created schedule record for game {EspnGameId}", schedule.EspnGameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule record for game {EspnGameId}", schedule.EspnGameId);
            return false;
        }
    }

    /// <summary>
    /// Updates an existing schedule record
    /// </summary>
    /// <param name="schedule">Schedule record to update</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> UpdateScheduleAsync(Schedule schedule)
    {
        try
        {
            schedule.UpdatedAt = DateTime.UtcNow;

            await _supabaseClient
                .From<Schedule>()
                .Where(s => s.Id == schedule.Id)
                .Update(schedule);

            _logger.LogDebug("Successfully updated schedule record {ScheduleId}", schedule.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule record {ScheduleId}", schedule.Id);
            return false;
        }
    }

    /// <summary>
    /// Gets a team by ID
    /// </summary>
    /// <param name="teamId">Team ID</param>
    /// <returns>Team if found, null otherwise</returns>
    public async Task<Team?> GetTeamByIdAsync(long teamId)
    {
        try
        {
            var result = await _supabaseClient
                .From<Team>()
                .Where(t => t.Id == teamId)
                .Get();

            return result.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team by ID {TeamId}", teamId);
            return null;
        }
    }

    /// <summary>
    /// Cleanup method for the service (Supabase client doesn't implement IDisposable)
    /// </summary>
    public void Cleanup()
    {
        // Supabase client cleanup if needed
        // The client will be cleaned up by GC
    }
}