using ESPNScrape.Models;

namespace ESPNScrape.Services;

public interface IESPNDataService
{
    Task<List<Team>> GetTeamsAsync();
    Task<Team?> GetTeamAsync(string teamId);
    Task<List<Game>> GetWeeklyGamesAsync(int year, int seasonType, int week);
    Task<List<Game>> GetNFLWeekGamesAsync(int year, int week);
    Task<Team?> GetTeamFromUrlAsync(string url);
    Task<Game?> GetGameAsync(string gameId);
    Task<Player?> GetPlayerAsync(string playerId);
    Task<List<Player>> GetTeamRosterAsync(string teamId, int year);
    Task<List<Player>> GetTeamRosterAsync(int year, string teamId);
    Task<List<Team>> GetNFLTeamsAsync(int year);
    Task<List<Odds>> GetGameOddsAsync(string gameId, string competitionId);
    Task<Odds?> GetOddsAsync(string oddsUrl);
    Task<GameSummary?> GetGameSummaryAsync(string gameId);
}
