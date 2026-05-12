using ESPNScrape.Models;

namespace ESPNScrape.Services;

public interface IESPNGameSource
{
    Task<List<Game>> GetNFLWeekGamesAsync(int year, int week);
    Task<GameSummary?> GetGameSummaryAsync(string gameId);
    Task<List<Odds>> GetGameOddsAsync(string gameId, string competitionId);
    Task<Odds?> GetOddsAsync(string oddsUrl);
}
