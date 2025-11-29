using ESPNScrape.Models.Supa;

namespace ESPNScrape.Services;

public interface ISupabaseService
{
    Task<List<Player>> GetPlayersAsync(string? espnPlayerId = null);
    Task<List<Player>> GetPlayersByTeamIdAsync(int teamId);
    Task<Player?> GetPlayerByEspnIdAsync(string espnPlayerId);
    Task<List<Player>> SearchPlayersByNameAsync(string firstName, string lastName, string? teamAbbreviation = null);
    Task<bool> UpsertPlayerStatAsync(PlayerStat playerStat);
    Task<int> UpsertPlayerStatsBatchAsync(IEnumerable<PlayerStat> playerStats);
    Task<List<PlayerStat>> GetPlayerStatsAsync(string playerCode, DateTime? startDate = null, DateTime? endDate = null);
    Task<bool> UpdatePlayerEspnIdAsync(long playerId, string espnPlayerId);
    Task<Team?> GetTeamByAbbreviationAsync(string abbreviation);
    Task<bool> UpdatePlayerAsync(Player player);
    Task<(bool Success, string? PublicUrl, string? Error)> UploadImageAsync(string bucketName, string path, byte[] imageData);
    Task<Schedule?> GetScheduleByEspnGameIdAsync(string espnGameId);
    Task<bool> CreateScheduleAsync(Schedule schedule);
    Task<bool> UpdateScheduleAsync(Schedule schedule);
    Task<Team?> GetTeamByIdAsync(long teamId);
    void Cleanup();
}
