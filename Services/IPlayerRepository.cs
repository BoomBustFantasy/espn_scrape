using ESPNScrape.Models.Supa;

namespace ESPNScrape.Services;

public interface IPlayerRepository
{
    Task<List<Player>> GetPlayersAsync(string? espnPlayerId = null);
    Task<List<Player>> GetPlayersByTeamIdAsync(int teamId);
    Task<Player?> GetPlayerByEspnIdAsync(string espnPlayerId);
    Task<bool> UpdatePlayerAsync(Player player);
    Task<bool> UpdatePlayerEspnIdAsync(long playerId, string espnPlayerId);
}
