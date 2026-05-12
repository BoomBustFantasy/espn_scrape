using ESPNScrape.Models.Supa;
using Microsoft.Extensions.Logging;
using Supabase;

namespace ESPNScrape.Services.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly Client _supabaseClient;
    private readonly ILogger<PlayerRepository> _logger;

    public PlayerRepository(Client supabaseClient, ILogger<PlayerRepository> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

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
            var existingPlayer = await _supabaseClient
                .From<Player>()
                .Where(p => p.Id == playerId)
                .Single();

            if (existingPlayer == null)
            {
                _logger.LogWarning("Player {PlayerId} not found in database", playerId);
                return false;
            }

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
}
