using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
using ESPNScrape.Utils;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ESPNScrape.Jobs;

[DisallowConcurrentExecution]
public class NFLWeeklyJob : IJob
{
    private readonly ILogger<NFLWeeklyJob> _logger;
    private readonly IESPNGameSource _espnGameSource;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPlayerStatRepository _playerStatRepository;

    public NFLWeeklyJob(ILogger<NFLWeeklyJob> logger, IESPNGameSource espnGameSource,
        IPlayerRepository playerRepository, IPlayerStatRepository playerStatRepository)
    {
        _logger = logger;
        _espnGameSource = espnGameSource;
        _playerRepository = playerRepository;
        _playerStatRepository = playerStatRepository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting NFL Weekly games scraping job");

        var totalGamesProcessed = 0;
        var totalPlayersFound = 0;
        var totalPlayersMatched = 0;
        var totalRecordsProcessed = 0;

        try
        {
            var jobDataMap = context.MergedJobDataMap;
            int currentSeason;
            List<int> weeksToCheck;

            if (jobDataMap.ContainsKey("season"))
            {
                currentSeason = jobDataMap.GetInt("season");
                var startWeek = jobDataMap.ContainsKey("startWeek") ? jobDataMap.GetInt("startWeek") : 1;
                var endWeek = jobDataMap.ContainsKey("endWeek") ? jobDataMap.GetInt("endWeek") : 18;

                weeksToCheck = Enumerable.Range(startWeek, endWeek - startWeek + 1).ToList();

                _logger.LogInformation("Using explicit season {Season}, weeks {StartWeek}-{EndWeek} from job data",
                    currentSeason, startWeek, endWeek);
            }
            else
            {
                var phase = await _espnGameSource.GetCurrentSeasonPhaseAsync();

                if (phase == null)
                {
                    // ESPN unreachable - fall back to the date estimate. That estimate is tuned to
                    // a past season's calendar and drifts, so treat this as a stopgap, not a substitute.
                    currentSeason = NFLCalendar.GetCurrentSeason();
                    weeksToCheck = NFLCalendar.GetWeeksToCheck(currentSeason, DateTime.UtcNow);
                    _logger.LogWarning("Could not read season phase from ESPN - falling back to estimated season {Season}, weeks [{Weeks}]",
                        currentSeason, string.Join(", ", weeksToCheck));
                }
                else if (phase.IsRegularSeason)
                {
                    currentSeason = phase.Season;
                    weeksToCheck = NFLCalendar.WeekWindow(phase.Week);
                    _logger.LogInformation("ESPN reports regular season {Season} week {Week} - checking weeks [{Weeks}]",
                        currentSeason, phase.Week, string.Join(", ", weeksToCheck));
                }
                else if (phase.IsPostseason)
                {
                    // Playoff games aren't scraped - GetNFLWeekGamesAsync only covers season type 2 -
                    // but week 18 stats can still be corrected after the postseason begins.
                    currentSeason = phase.Season;
                    weeksToCheck = [18];
                    _logger.LogInformation("ESPN reports postseason for {Season} - re-checking regular season week 18 only",
                        currentSeason);
                }
                else
                {
                    _logger.LogInformation("ESPN reports season type {SeasonType} for {Season} - not the regular season, nothing to scrape",
                        phase.SeasonType, phase.Season);
                    return;
                }
            }

            // Load the full player list once for the entire job run
            var allPlayers = await _playerRepository.GetPlayersAsync();
            _logger.LogInformation("Loaded {PlayerCount} players from database", allPlayers.Count);

            foreach (var week in weeksToCheck)
            {
                _logger.LogInformation("=== PROCESSING WEEK {Week} ===", week);

                var games = await _espnGameSource.GetNFLWeekGamesAsync(currentSeason, week);

                if (games == null || !games.Any())
                {
                    _logger.LogInformation("No games found for NFL {Season} Week {Week}", currentSeason, week);
                    await Task.Delay(500);
                    continue;
                }

                _logger.LogInformation("Found {GameCount} games for NFL {Season} Week {Week}",
                    games.Count(), currentSeason, week);

                foreach (var game in games)
                {
                    var (gamePlayersFound, gamePlayersMatched, gameRecordsProcessed) =
                        await ProcessGameAsync(game, currentSeason, week, allPlayers);
                    totalGamesProcessed++;
                    totalPlayersFound += gamePlayersFound;
                    totalPlayersMatched += gamePlayersMatched;
                    totalRecordsProcessed += gameRecordsProcessed;
                }

                _logger.LogInformation("=== COMPLETED WEEK {Week} ===", week);
                await Task.Delay(500);
            }

            _logger.LogInformation("🏆 NFL WEEKLY JOB SUMMARY: {TotalGames} games processed | {TotalPlayersFound} players found | {TotalPlayersMatched} matched to DB | {TotalRecordsProcessed} records processed",
                totalGamesProcessed, totalPlayersFound, totalPlayersMatched, totalRecordsProcessed);
            _logger.LogInformation("Completed NFL Weekly games scraping job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while scraping NFL Weekly games");
            throw;
        }
    }

    private async Task<(int playersFound, int playersMatched, int recordsProcessed)> ProcessGameAsync(
        Game game, int season, int week, IReadOnlyList<Models.Supa.Player> allPlayers)
    {
        try
        {
            if (game.Competitions?.Any() != true)
            {
                _logger.LogWarning("Game {GameId} has no competitions", game.Id);
                return (0, 0, 0);
            }

            var competition = game.Competitions.First();
            var homeTeam = competition.Competitors?.FirstOrDefault(c => c.HomeAway?.ToLower() == "home");
            var awayTeam = competition.Competitors?.FirstOrDefault(c => c.HomeAway?.ToLower() == "away");

            var parsedStats = await _espnGameSource.GetBoxScoreStatsAsync(competition, season, week);

            if (parsedStats.Count == 0)
            {
                _logger.LogWarning("No player stats parsed for game {GameId}", game.Id);
                return (0, 0, 0);
            }

            var totalFound = parsedStats.Count;
            var totalMatched = 0;
            var playerStatsToUpsert = new List<Models.Supa.PlayerStat>();

            foreach (var stat in parsedStats)
            {
                var supabasePlayerId = PlayerResolver.Resolve(allPlayers, stat.EspnPlayerId, stat.EspnTeamId, stat.Name);

                if (supabasePlayerId == null)
                {
                    _logger.LogDebug("Could not resolve player {PlayerName} (ESPN ID: {EspnId}, team ESPN ID: {TeamId})",
                        stat.Name, stat.EspnPlayerId, stat.EspnTeamId);
                }
                else
                {
                    totalMatched++;
                }

                playerStatsToUpsert.Add(new Models.Supa.PlayerStat
                {
                    PlayerCode = $"ESPN_{stat.EspnPlayerId}",
                    Name = stat.Name,
                    Team = stat.TeamDisplayName,
                    EspnPlayerId = stat.EspnPlayerId,
                    EspnGameId = stat.EspnGameId,
                    PlayerId = supabasePlayerId,
                    Season = stat.Season,
                    Week = stat.Week,
                    GameDate = stat.GameDate,
                    GameLocation = "",
                    Passing = stat.Passing,
                    Rushing = stat.Rushing,
                    Receiving = stat.Receiving,
                    Fumbles = stat.Fumbles,
                    FumblesLost = stat.FumblesLost
                });
            }

            var upsertedCount = 0;
            try
            {
                upsertedCount = await _playerStatRepository.UpsertPlayerStatsBatchAsync(playerStatsToUpsert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting player stats for game {GameId}", game.Id);
            }

            _logger.LogInformation("Game {GameId}: {TotalFound} players found | {TotalMatched} matched | {Upserted} records upserted",
                game.Id, totalFound, totalMatched, upsertedCount);

            return (totalFound, totalMatched, upsertedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing game {GameId}", game.Id);
            return (0, 0, 0);
        }
    }

}