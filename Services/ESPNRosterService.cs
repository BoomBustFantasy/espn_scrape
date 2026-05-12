using ESPNScrape.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ESPNScrape.Services;

public class ESPNRosterService : ESPNClientBase, IESPNRosterSource
{
    private readonly ILogger<ESPNRosterService> _logger;

    private const string BaseApiUrl = "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl";

    public ESPNRosterService(HttpClient httpClient, ILogger<ESPNRosterService> logger)
        : base(httpClient, logger)
    {
        _logger = logger;
    }

    public async Task<List<Team>> GetNFLTeamsAsync(int year)
    {
        _logger.LogInformation("Fetching NFL teams for season {Year}", year);
        var url = $"{BaseApiUrl}/seasons/{year}/teams";
        var teams = await FetchPagedReferencesAsync<Team>(url);
        _logger.LogInformation("Successfully fetched {TeamCount} teams for season {Year}", teams.Count, year);
        return teams;
    }

    public async Task<List<Player>> GetTeamRosterAsync(string teamId, int year)
    {
        _logger.LogInformation("Fetching roster for team {TeamId}, year {Year}", teamId, year);
        var url = $"{BaseApiUrl}/seasons/{year}/teams/{teamId}/athletes";
        var players = await FetchPagedReferencesAsync<Player>(url);
        _logger.LogInformation("Successfully fetched {PlayerCount} players for team {TeamId}", players.Count, teamId);
        return players;
    }

    public async Task<Player?> GetPlayerAsync(string playerId)
    {
        try
        {
            _logger.LogInformation("Fetching player {PlayerId} from ESPN API", playerId);
            var response = await _httpClient.GetStringAsync($"{BaseApiUrl}/athletes/{playerId}");
            var player = JsonSerializer.Deserialize<Player>(response, JsonOptions);
            _logger.LogInformation("Successfully fetched player {PlayerId}", playerId);
            return player;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching player {PlayerId} from ESPN API", playerId);
            return null;
        }
    }
}
