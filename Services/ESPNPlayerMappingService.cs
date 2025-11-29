using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using Microsoft.Extensions.Logging;

namespace ESPNScrape.Services;

/// <summary>
/// Service for mapping ESPN player data to Supabase database players
/// </summary>
public interface IESPNPlayerMappingService
{
    Task<Models.Supa.Player?> FindPlayerByEspnIdAsync(string espnPlayerId);
    Task<Models.Supa.Player?> MapEspnPlayerToSupabaseAsync(PlayerInfo espnPlayer, string? teamName = null);
    Task<List<Models.Supa.Player>> FindPlayersByNameAsync(string firstName, string lastName);
    Task<bool> UpdatePlayerEspnIdAsync(long playerId, string espnPlayerId);
}

public class ESPNPlayerMappingService : IESPNPlayerMappingService
{
    private readonly ILogger<ESPNPlayerMappingService> _logger;
    private readonly ISupabaseService _supabaseService;

    public ESPNPlayerMappingService(ILogger<ESPNPlayerMappingService> logger, ISupabaseService supabaseService)
    {
        _logger = logger;
        _supabaseService = supabaseService;
    }

    /// <summary>
    /// Finds a player in the database by ESPN player ID
    /// </summary>
    public async Task<Models.Supa.Player?> FindPlayerByEspnIdAsync(string espnPlayerId)
    {
        try
        {
            _logger.LogDebug("Looking up player with ESPN ID: {EspnPlayerId}", espnPlayerId);

            var player = await _supabaseService.GetPlayerByEspnIdAsync(espnPlayerId);
            return player;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding player by ESPN ID: {EspnPlayerId}", espnPlayerId);
            return null;
        }
    }

    /// <summary>
    /// Maps ESPN player data to an existing Supabase player or creates a new mapping
    /// </summary>
    public async Task<Models.Supa.Player?> MapEspnPlayerToSupabaseAsync(PlayerInfo espnPlayer, string? teamName = null)
    {
        try
        {
            if (espnPlayer?.Id == null)
            {
                _logger.LogWarning("ESPN player data is null or missing ID");
                return null;
            }

            // First, try to find by ESPN ID
            var existingPlayer = await FindPlayerByEspnIdAsync(espnPlayer.Id);
            if (existingPlayer != null)
            {
                _logger.LogDebug("Found existing player mapping: {PlayerName} (ESPN ID: {EspnId})",
                    $"{existingPlayer.FirstName} {existingPlayer.LastName}", espnPlayer.Id);
                return existingPlayer;
            }

            // Try to find by name matching
            var nameCandidates = await FindPlayersByNameAsync(
                espnPlayer.FirstName ?? "",
                espnPlayer.LastName ?? "");

            if (nameCandidates.Count == 1)
            {
                // Exact match found, try to update with ESPN ID
                var player = nameCandidates.First();
                var updateSuccess = await UpdatePlayerEspnIdAsync(player.Id, espnPlayer.Id);

                if (updateSuccess)
                {
                    _logger.LogInformation("Mapped existing player {PlayerName} to ESPN ID {EspnId}",
                        $"{player.FirstName} {player.LastName}", espnPlayer.Id);
                    player.EspnPlayerId = espnPlayer.Id;
                }
                else
                {
                    _logger.LogWarning("Failed to update ESPN ID for player {PlayerName} (ID: {PlayerId}), but returning player anyway",
                        $"{player.FirstName} {player.LastName}", player.Id);
                }

                return player;
            }
            else if (nameCandidates.Count > 1)
            {
                _logger.LogWarning("Multiple players found with name {FirstName} {LastName}, manual review needed",
                    espnPlayer.FirstName, espnPlayer.LastName);

                // TODO: Implement more sophisticated matching logic
                // Could match by team, position, etc.
                return null;
            }

            // No existing player found, do not create new players
            _logger.LogInformation("No existing player found for ESPN player: {PlayerName} (ESPN ID: {EspnId})",
                espnPlayer.DisplayName, espnPlayer.Id);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping ESPN player {PlayerId} ({PlayerName})",
                espnPlayer?.Id, espnPlayer?.DisplayName);
            return null;
        }
    }

    /// <summary>
    /// Finds players by first and last name
    /// </summary>
    public async Task<List<Models.Supa.Player>> FindPlayersByNameAsync(string firstName, string lastName)
    {
        try
        {
            _logger.LogDebug("Searching for players with name: {FirstName} {LastName}", firstName, lastName);

            var players = await _supabaseService.SearchPlayersByNameAsync(firstName, lastName);
            return players;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding players by name: {FirstName} {LastName}", firstName, lastName);
            return new List<Models.Supa.Player>();
        }
    }

    /// <summary>
    /// Updates a player's ESPN ID in the database
    /// </summary>
    public async Task<bool> UpdatePlayerEspnIdAsync(long playerId, string espnPlayerId)
    {
        try
        {
            _logger.LogDebug("Updating player {PlayerId} with ESPN ID: {EspnPlayerId}", playerId, espnPlayerId);

            var success = await _supabaseService.UpdatePlayerEspnIdAsync(playerId, espnPlayerId);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId} with ESPN ID {EspnPlayerId}", playerId, espnPlayerId);
            return false;
        }
    }

    /// <summary>
    /// Maps ESPN position name to Supabase position ID
    /// </summary>
    private long? MapEspnPositionToSupabaseId(string? positionName)
    {
        if (string.IsNullOrEmpty(positionName))
            return null;

        // TODO: Query your Positions table to get actual mappings
        // For now, return null - you'll need to implement this based on your Positions table
        return positionName?.ToUpper() switch
        {
            "QB" => 1,  // Quarterback
            "RB" => 2,  // Running Back
            "WR" => 3,  // Wide Receiver
            "TE" => 4,  // Tight End
            "K" => 5,   // Kicker
            _ => null
        };
    }

    /// <summary>
    /// Batch maps multiple ESPN players from a game
    /// </summary>
    public async Task<Dictionary<string, Models.Supa.Player?>> MapGamePlayersAsync(
        List<PlayerInfo> espnPlayers,
        string? teamName = null)
    {
        var mappedPlayers = new Dictionary<string, Models.Supa.Player?>();

        foreach (var espnPlayer in espnPlayers)
        {
            if (!string.IsNullOrEmpty(espnPlayer.Id))
            {
                var mappedPlayer = await MapEspnPlayerToSupabaseAsync(espnPlayer, teamName);
                mappedPlayers[espnPlayer.Id] = mappedPlayer;
            }
        }

        _logger.LogInformation("Mapped {MappedCount} of {TotalCount} players for team {TeamName}",
            mappedPlayers.Count(p => p.Value != null),
            espnPlayers.Count,
            teamName ?? "Unknown");

        return mappedPlayers;
    }
}