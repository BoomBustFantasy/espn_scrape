using ESPNScrape.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ESPNScrape.Services;

public class ESPNGameService : ESPNClientBase, IESPNGameSource
{
    private readonly ILogger<ESPNGameService> _logger;

    private const string BaseApiUrl = "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl";
    private const string SiteApiUrl = "https://site.api.espn.com/apis/site/v2/sports/football/nfl";

    public ESPNGameService(HttpClient httpClient, ILogger<ESPNGameService> logger)
        : base(httpClient, logger)
    {
        _logger = logger;
    }

    public async Task<List<Game>> GetNFLWeekGamesAsync(int year, int week)
    {
        _logger.LogInformation("Fetching NFL regular season games for Year: {Year}, Week: {Week}", year, week);
        var url = $"{BaseApiUrl}/seasons/{year}/types/2/weeks/{week}/events";
        var games = await FetchPagedReferencesAsync<Game>(url);
        _logger.LogInformation("Successfully fetched {GameCount} games for Year: {Year}, Week: {Week}", games.Count, year, week);
        return games;
    }

    public async Task<GameSummary?> GetGameSummaryAsync(string gameId)
    {
        try
        {
            _logger.LogInformation("Fetching game summary for game {GameId}", gameId);
            var url = $"{SiteApiUrl}/summary?event={gameId}";
            var response = await _httpClient.GetStringAsync(url);
            var gameSummary = JsonSerializer.Deserialize<GameSummary>(response, JsonOptions);
            _logger.LogInformation("Successfully fetched game summary for game {GameId}", gameId);
            return gameSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching game summary for game {GameId}", gameId);
            return null;
        }
    }

    public async Task<List<Odds>> GetGameOddsAsync(string gameId, string competitionId)
    {
        try
        {
            _logger.LogInformation("Fetching odds for game {GameId}, competition {CompetitionId}", gameId, competitionId);
            var url = $"{BaseApiUrl}/events/{gameId}/competitions/{competitionId}/odds";
            var response = await _httpClient.GetStringAsync(url);
            var apiResponse = JsonSerializer.Deserialize<ESPNApiResponse<Odds>>(response, JsonOptions);
            var odds = apiResponse?.Items ?? new List<Odds>();
            _logger.LogInformation("Successfully fetched {OddsCount} odds for game {GameId}", odds.Count, gameId);
            return odds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching odds for game {GameId}, competition {CompetitionId}", gameId, competitionId);
            return new List<Odds>();
        }
    }

    public async Task<Odds?> GetOddsAsync(string oddsUrl)
    {
        try
        {
            _logger.LogDebug("Fetching odds data from URL: {OddsUrl}", oddsUrl);
            var response = await _httpClient.GetStringAsync(oddsUrl);
            var apiResponse = JsonSerializer.Deserialize<ESPNApiResponse<Odds>>(response, JsonOptions);
            var odds = apiResponse?.Items?.FirstOrDefault();
            if (odds != null)
            {
                _logger.LogDebug("Successfully fetched odds data from {OddsUrl} - Provider: {Provider}, O/U: {OverUnder}, Spread: {Spread}",
                    oddsUrl, odds.Provider?.Name ?? "Unknown", odds.OverUnder, odds.Spread);
            }
            else
            {
                _logger.LogWarning("No odds data found at {OddsUrl}", oddsUrl);
            }
            return odds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching odds from URL: {OddsUrl}", oddsUrl);
            return null;
        }
    }

    public async Task<StatisticsResponse?> GetCompetitorStatisticsAsync(string statsRefUrl)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(statsRefUrl);
            return JsonSerializer.Deserialize<StatisticsResponse>(response, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching competitor statistics from URL: {StatsRefUrl}", statsRefUrl);
            return null;
        }
    }

    public async Task<StatisticsResponse?> GetAthleteStatisticsAsync(string statsRefUrl)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(statsRefUrl);
            return JsonSerializer.Deserialize<StatisticsResponse>(response, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching athlete statistics from URL: {StatsRefUrl}", statsRefUrl);
            return null;
        }
    }

    private async Task<string> GetAthleteNameAsync(string athleteRefUrl)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(athleteRefUrl);
            var athlete = JsonSerializer.Deserialize<Player>(response, JsonOptions);
            return athlete?.DisplayName ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching athlete name from URL: {AthleteRefUrl}", athleteRefUrl);
            return string.Empty;
        }
    }

    public async Task<List<ParsedPlayerStat>> GetBoxScoreStatsAsync(Competition competition, int season, int week)
    {
        var results = new List<ParsedPlayerStat>();

        foreach (var competitor in competition.Competitors)
        {
            var statsUrl = competitor.Statistics?.GetUrl();
            if (string.IsNullOrEmpty(statsUrl))
                continue;

            var teamStats = await GetCompetitorStatisticsAsync(statsUrl);
            if (teamStats == null)
                continue;

            var relevantAthletes = CoreApiBoxScoreMapper.ExtractRelevantAthletes(teamStats);
            var teamDisplayName = ESPNTeamMapper.GetAllTeamMappings().TryGetValue(competitor.Id, out var mapping)
                ? mapping.FullName
                : string.Empty;

            foreach (var athleteRef in relevantAthletes)
            {
                var athleteStatsUrl = athleteRef.Statistics?.GetUrl();
                var athleteRefUrl = athleteRef.Athlete?.GetUrl();
                if (string.IsNullOrEmpty(athleteStatsUrl) || string.IsNullOrEmpty(athleteRefUrl))
                    continue;

                var espnPlayerId = CoreApiBoxScoreMapper.ExtractAthleteId(athleteRefUrl);
                if (string.IsNullOrEmpty(espnPlayerId))
                    continue;

                var athleteStats = await GetAthleteStatisticsAsync(athleteStatsUrl);
                var athleteName = await GetAthleteNameAsync(athleteRefUrl);
                if (athleteStats != null)
                {
                    results.Add(CoreApiBoxScoreMapper.Map(
                        athleteStats,
                        espnPlayerId,
                        competitor.Id,
                        teamDisplayName,
                        athleteName,
                        competition.Id,
                        competition.Date,
                        season,
                        week));
                }

                await Task.Delay(150);
            }
        }

        return results;
    }
}
