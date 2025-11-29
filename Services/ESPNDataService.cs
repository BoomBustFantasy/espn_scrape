using ESPNScrape.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESPNScrape.Services;

public class ESPNDataService : IESPNDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ESPNDataService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string BaseApiUrl = "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl";
    private const string SiteApiUrl = "https://site.api.espn.com/apis/site/v2/sports/football/nfl";

    public ESPNDataService(HttpClient httpClient, ILogger<ESPNDataService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<Team>> GetTeamsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching NFL teams from ESPN API");

            var allTeams = new List<Team>();
            var currentPage = 1;
            var totalPages = 1;

            // Fetch all pages of teams
            do
            {
                var url = $"{BaseApiUrl}/teams?page={currentPage}";
                var response = await _httpClient.GetStringAsync(url);
                var apiResponse = JsonSerializer.Deserialize<ESPNReferenceResponse>(response, _jsonOptions);

                if (apiResponse?.Items == null)
                    break;

                // Update total pages from first response
                if (currentPage == 1)
                {
                    totalPages = apiResponse.PageCount > 0 ? apiResponse.PageCount : 1;
                    _logger.LogInformation("Found {TotalCount} total NFL teams across {PageCount} pages",
                        apiResponse.Count, totalPages);
                }

                // ESPN returns references, so we need to fetch each team individually
                foreach (var teamRefObj in apiResponse.Items)
                {
                    try
                    {
                        var teamResponse = await _httpClient.GetStringAsync(teamRefObj.GetUrl());
                        var team = JsonSerializer.Deserialize<Team>(teamResponse, _jsonOptions);
                        if (team != null)
                            allTeams.Add(team);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch team from reference: {TeamRef}", teamRefObj.GetUrl());
                    }

                    // Add delay to prevent rate limiting
                    await Task.Delay(100);
                }

                _logger.LogInformation("Fetched page {CurrentPage}/{TotalPages} with {PageTeamCount} teams",
                    currentPage, totalPages, apiResponse.Items.Count);

                currentPage++;

            } while (currentPage <= totalPages);

            _logger.LogInformation("Successfully fetched {TeamCount} total teams", allTeams.Count);
            return allTeams;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching teams from ESPN API");
            return new List<Team>();
        }
    }

    public async Task<Team?> GetTeamAsync(string teamId)
    {
        try
        {
            _logger.LogInformation("Fetching team {TeamId} from ESPN API", teamId);

            var response = await _httpClient.GetStringAsync($"{BaseApiUrl}/seasons/2025/teams/{teamId}");
            var team = JsonSerializer.Deserialize<Team>(response, _jsonOptions);

            _logger.LogInformation("Successfully fetched team {TeamId}", teamId);
            return team;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching team {TeamId} from ESPN API", teamId);
            return null;
        }
    }

    public async Task<List<Game>> GetWeeklyGamesAsync(int year, int seasonType, int week)
    {
        try
        {
            _logger.LogInformation("Fetching games for Year: {Year}, SeasonType: {SeasonType}, Week: {Week}",
                year, seasonType, week);

            var url = $"{BaseApiUrl}/seasons/{year}/types/{seasonType}/weeks/{week}/events";
            var response = await _httpClient.GetStringAsync(url);
            var apiResponse = JsonSerializer.Deserialize<ESPNReferenceResponse>(response, _jsonOptions);

            if (apiResponse?.Items == null)
                return new List<Game>();

            var games = new List<Game>();

            // ESPN returns references, so we need to fetch each game individually
            foreach (var gameRefObj in apiResponse.Items)
            {
                try
                {
                    var gameResponse = await _httpClient.GetStringAsync(gameRefObj.GetUrl());
                    var game = JsonSerializer.Deserialize<Game>(gameResponse, _jsonOptions);
                    if (game != null)
                        games.Add(game);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch game from reference: {GameRef}", gameRefObj.GetUrl());
                }

                // Add delay to prevent rate limiting
                await Task.Delay(100);
            }

            _logger.LogInformation("Successfully fetched {GameCount} games", games.Count);
            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching games from ESPN API for Year: {Year}, SeasonType: {SeasonType}, Week: {Week}",
                year, seasonType, week);
            return new List<Game>();
        }
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        try
        {
            _logger.LogInformation("Fetching game {GameId} from ESPN API", gameId);

            var response = await _httpClient.GetStringAsync($"{BaseApiUrl}/events/{gameId}");
            var game = JsonSerializer.Deserialize<Game>(response, _jsonOptions);

            _logger.LogInformation("Successfully fetched game {GameId}", gameId);
            return game;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching game {GameId} from ESPN API", gameId);
            return null;
        }
    }

    public async Task<Player?> GetPlayerAsync(string playerId)
    {
        try
        {
            _logger.LogInformation("Fetching player {PlayerId} from ESPN API", playerId);

            var response = await _httpClient.GetStringAsync($"{BaseApiUrl}/athletes/{playerId}");
            var player = JsonSerializer.Deserialize<Player>(response, _jsonOptions);

            _logger.LogInformation("Successfully fetched player {PlayerId}", playerId);
            return player;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching player {PlayerId} from ESPN API", playerId);
            return null;
        }
    }

    public async Task<List<Player>> GetTeamRosterAsync(string teamId, int year)
    {
        try
        {
            _logger.LogInformation("Fetching roster for team {TeamId}, year {Year}", teamId, year);

            var allPlayers = new List<Player>();
            var currentPage = 1;
            var totalPages = 1;

            // Fetch all pages of players for the team
            do
            {
                var url = $"{BaseApiUrl}/seasons/{year}/teams/{teamId}/athletes?page={currentPage}";
                var response = await _httpClient.GetStringAsync(url);
                var apiResponse = JsonSerializer.Deserialize<ESPNReferenceResponse>(response, _jsonOptions);

                if (apiResponse?.Items == null)
                    break;

                // Update total pages from first response
                if (currentPage == 1)
                {
                    totalPages = apiResponse.PageCount > 0 ? apiResponse.PageCount : 1;
                    _logger.LogInformation("Team {TeamId} has {TotalCount} total players across {PageCount} pages",
                        teamId, apiResponse.Count, totalPages);
                }

                // Process all players on this page
                foreach (var playerRefObj in apiResponse.Items)
                {
                    try
                    {
                        var playerResponse = await _httpClient.GetStringAsync(playerRefObj.GetUrl());
                        var player = JsonSerializer.Deserialize<Player>(playerResponse, _jsonOptions);
                        if (player != null)
                            allPlayers.Add(player);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch player from reference: {PlayerRef}", playerRefObj.GetUrl());
                    }

                    // Add delay to prevent rate limiting
                    await Task.Delay(100);
                }

                _logger.LogInformation("Fetched page {CurrentPage}/{TotalPages} with {PagePlayerCount} players for team {TeamId}",
                    currentPage, totalPages, apiResponse.Items.Count, teamId);

                currentPage++;

            } while (currentPage <= totalPages);

            _logger.LogInformation("Successfully fetched {PlayerCount} total players for team {TeamId}", allPlayers.Count, teamId);
            return allPlayers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching roster for team {TeamId}, year {Year}", teamId, year);
            return new List<Player>();
        }
    }

    public async Task<List<Game>> GetNFLWeekGamesAsync(int year, int week)
    {
        // For NFL, season type 2 is regular season
        return await GetWeeklyGamesAsync(year, 2, week);
    }

    public async Task<List<Player>> GetTeamRosterAsync(int year, string teamId)
    {
        // Overload to match the signature expected by NFLPlayerHeadshotJob
        return await GetTeamRosterAsync(teamId, year);
    }

    public async Task<List<Team>> GetNFLTeamsAsync(int year)
    {
        try
        {
            _logger.LogInformation("Fetching NFL teams for season {Year}", year);

            var allTeams = new List<Team>();
            var currentPage = 1;
            var totalPages = 1;

            // Fetch all pages of teams
            do
            {
                var url = $"{BaseApiUrl}/seasons/{year}/teams?page={currentPage}";
                var response = await _httpClient.GetStringAsync(url);
                var apiResponse = JsonSerializer.Deserialize<ESPNReferenceResponse>(response, _jsonOptions);

                if (apiResponse?.Items == null)
                    break;

                // Update total pages from first response
                if (currentPage == 1)
                {
                    totalPages = apiResponse.PageCount > 0 ? apiResponse.PageCount : 1;
                    _logger.LogInformation("Found {TotalCount} total NFL teams across {PageCount} pages for season {Year}",
                        apiResponse.Count, totalPages, year);
                }

                // ESPN returns references, so we need to fetch each team individually
                foreach (var teamRefObj in apiResponse.Items)
                {
                    try
                    {
                        var teamResponse = await _httpClient.GetStringAsync(teamRefObj.GetUrl());
                        var team = JsonSerializer.Deserialize<Team>(teamResponse, _jsonOptions);
                        if (team != null)
                            allTeams.Add(team);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch team from reference: {TeamRef}", teamRefObj.GetUrl());
                    }

                    // Add delay to prevent rate limiting
                    await Task.Delay(100);
                }

                _logger.LogInformation("Fetched page {CurrentPage}/{TotalPages} with {PageTeamCount} teams for season {Year}",
                    currentPage, totalPages, apiResponse.Items.Count, year);

                currentPage++;

            } while (currentPage <= totalPages);

            _logger.LogInformation("Successfully fetched {TeamCount} total teams for season {Year}", allTeams.Count, year);
            return allTeams;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NFL teams for season {Year}", year);
            return new List<Team>();
        }
    }

    public async Task<Team?> GetTeamFromUrlAsync(string url)
    {
        try
        {
            _logger.LogInformation("Fetching team from URL: {Url}", url);

            var response = await _httpClient.GetStringAsync(url);
            var team = JsonSerializer.Deserialize<Team>(response, _jsonOptions);

            _logger.LogInformation("Successfully fetched team from URL");
            return team;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching team from URL: {Url}", url);
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
            var apiResponse = JsonSerializer.Deserialize<ESPNApiResponse<Odds>>(response, _jsonOptions);

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
            var apiResponse = JsonSerializer.Deserialize<ESPNApiResponse<Odds>>(response, _jsonOptions);

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

    public async Task<GameSummary?> GetGameSummaryAsync(string gameId)
    {
        try
        {
            _logger.LogInformation("Fetching game summary for game {GameId}", gameId);

            var url = $"{SiteApiUrl}/summary?event={gameId}";
            var response = await _httpClient.GetStringAsync(url);
            var gameSummary = JsonSerializer.Deserialize<GameSummary>(response, _jsonOptions);

            _logger.LogInformation("Successfully fetched game summary for game {GameId}", gameId);
            return gameSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching game summary for game {GameId}", gameId);
            return null;
        }
    }
}

public class ESPNApiResponse<T>
{
    public int Count { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public List<T> Items { get; set; } = new();
}

// For handling ESPN reference responses
public class ESPNReferenceResponse
{
    public int Count { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public List<ESPNReference> Items { get; set; } = new();
}

public class ESPNReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public string GetUrl() => Ref;
}