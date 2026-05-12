using ESPNScrape.Models;

namespace ESPNScrape.Services;

public interface IESPNRosterSource
{
    Task<List<Team>> GetNFLTeamsAsync(int year);
    Task<List<Player>> GetTeamRosterAsync(string teamId, int year);
    Task<Player?> GetPlayerAsync(string playerId);
}
