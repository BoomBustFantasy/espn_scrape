using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
using ESPNScrape.Utils;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job that syncs NFL schedule data from ESPN API to the Schedule table
/// </summary>
[DisallowConcurrentExecution]
public class NFLScheduleSyncJob : IJob
{
    private readonly ILogger<NFLScheduleSyncJob> _logger;
    private readonly IESPNGameSource _espnGameSource;
    private readonly IScheduleRepository _scheduleRepository;

    public NFLScheduleSyncJob(
        ILogger<NFLScheduleSyncJob> logger,
        IESPNGameSource espnGameSource,
        IScheduleRepository scheduleRepository)
    {
        _logger = logger;
        _espnGameSource = espnGameSource;
        _scheduleRepository = scheduleRepository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("🗓️ Starting NFL Schedule Sync job");

        // Track overall job statistics
        var totalGamesProcessed = 0;
        var totalGamesUpdated = 0;
        var totalGamesCreated = 0;
        var totalErrors = 0;

        try
        {
            var jobDataMap = context.MergedJobDataMap;
            int currentSeason;
            List<int> weeksToSync;

            if (jobDataMap.ContainsKey("season"))
            {
                currentSeason = jobDataMap.GetInt("season");
                var startWeek = jobDataMap.ContainsKey("startWeek") ? jobDataMap.GetInt("startWeek") : 1;
                var endWeek = jobDataMap.ContainsKey("endWeek") ? jobDataMap.GetInt("endWeek") : 18;
                weeksToSync = Enumerable.Range(startWeek, endWeek - startWeek + 1).ToList();
                _logger.LogInformation("Using explicit season {Season}, weeks {StartWeek}-{EndWeek} from job data",
                    currentSeason, startWeek, endWeek);
            }
            else
            {
                currentSeason = NFLCalendar.GetCurrentSeason();
                weeksToSync = Enumerable.Range(1, 18).ToList();
                _logger.LogInformation("Syncing season {Season} weeks 1-18", currentSeason);
            }

            foreach (var week in weeksToSync)
            {
                _logger.LogInformation("Processing Regular Season Week {Week}", week);

                try
                {
                    var games = await _espnGameSource.GetNFLWeekGamesAsync(currentSeason, week);

                    if (games == null || !games.Any())
                    {
                        _logger.LogInformation("No games found for Regular Season {Season} Week {Week}", currentSeason, week);
                        continue;
                    }

                    _logger.LogInformation("Found {GameCount} games for Regular Season {Season} Week {Week}",
                        games.Count, currentSeason, week);

                    foreach (var game in games)
                    {
                        var (created, updated, error) = await ProcessGameSchedule(game, currentSeason, 2, week);

                        totalGamesProcessed++;
                        if (created) totalGamesCreated++;
                        if (updated) totalGamesUpdated++;
                        if (error) totalErrors++;
                    }
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    _logger.LogError(ex, "Error processing Regular Season Week {Week}", week);
                }

                await Task.Delay(500);
            }

            // Log overall job summary
            _logger.LogInformation("🗓️ NFL SCHEDULE SYNC SUMMARY: {TotalProcessed} games processed | {TotalCreated} created | {TotalUpdated} updated | {TotalErrors} errors",
                totalGamesProcessed, totalGamesCreated, totalGamesUpdated, totalErrors);
            _logger.LogInformation("✅ Completed NFL Schedule Sync job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Fatal error occurred while syncing NFL schedule");
            throw;
        }
    }

    private async Task<(bool created, bool updated, bool error)> ProcessGameSchedule(Game game, int season, int seasonType, int week)
    {
        try
        {
            if (string.IsNullOrEmpty(game.Id))
            {
                _logger.LogWarning("Game has no ID, skipping");
                return (false, false, true);
            }

            // Check if game already exists
            var existingSchedule = await _scheduleRepository.GetScheduleByEspnGameIdAsync(game.Id);

            // Extract team information
            var (homeTeamId, awayTeamId) = ExtractTeamIds(game);

            if (!homeTeamId.HasValue || !awayTeamId.HasValue)
            {
                _logger.LogWarning("Could not determine team IDs for game {GameId}", game.Id);
                return (false, false, true);
            }

            // Create or update schedule record
            var scheduleRecord = existingSchedule ?? new Models.Supa.Schedule();

            scheduleRecord.EspnGameId = game.Id;
            scheduleRecord.HomeTeamId = homeTeamId.Value;
            scheduleRecord.AwayTeamId = awayTeamId.Value;
            scheduleRecord.GameTime = game.Date;
            scheduleRecord.Week = week;
            scheduleRecord.Year = season;
            scheduleRecord.SeasonType = seasonType;

            // Extract betting information if available (from odds)
            await ExtractBettingInfo(game, scheduleRecord);

            bool created = false;
            bool updated = false;

            if (existingSchedule == null)
            {
                // Try to create new record, handle duplicate gracefully
                var success = await _scheduleRepository.CreateScheduleAsync(scheduleRecord);
                if (success)
                {
                    created = true;
                    _logger.LogInformation("✅ Created schedule record for game {GameId}: Team {AwayTeamId} @ Team {HomeTeamId}",
                        game.Id, awayTeamId.Value, homeTeamId.Value);
                }
                else
                {
                    // If create failed, try to get the existing record and update it
                    _logger.LogDebug("Create failed for game {GameId}, attempting to update existing record", game.Id);
                    var existingRecord = await _scheduleRepository.GetScheduleByEspnGameIdAsync(game.Id);
                    if (existingRecord != null)
                    {
                        // Copy the ID and update
                        scheduleRecord.Id = existingRecord.Id;
                        var updateSuccess = await _scheduleRepository.UpdateScheduleAsync(scheduleRecord);
                        if (updateSuccess)
                        {
                            updated = true;
                            _logger.LogDebug("🔄 Updated existing schedule record for game {GameId}", game.Id);
                        }
                        else
                        {
                            _logger.LogError("❌ Failed to update existing schedule record for game {GameId}", game.Id);
                            return (false, false, true);
                        }
                    }
                    else
                    {
                        _logger.LogError("❌ Failed to create or find existing schedule record for game {GameId}", game.Id);
                        return (false, false, true);
                    }
                }
            }
            else
            {
                // Update existing record
                var success = await _scheduleRepository.UpdateScheduleAsync(scheduleRecord);
                if (success)
                {
                    updated = true;
                    _logger.LogDebug("🔄 Updated schedule record for game {GameId}", game.Id);
                }
                else
                {
                    _logger.LogError("❌ Failed to update schedule record for game {GameId}", game.Id);
                    return (false, false, true);
                }
            }

            return (created, updated, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing game schedule for {GameId}", game?.Id ?? "unknown");
            return (false, false, true);
        }
    }

    private (long? homeTeamId, long? awayTeamId) ExtractTeamIds(Game game)
    {
        try
        {
            if (game.Competitions?.Any() != true)
                return (null, null);

            var competition = game.Competitions.First();
            if (competition.Competitors?.Count != 2)
                return (null, null);

            var homeCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway?.ToLower() == "home");
            var awayCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway?.ToLower() == "away");

            if (homeCompetitor?.Team == null || awayCompetitor?.Team == null)
                return (null, null);

            // Parse ESPN team IDs directly from the $ref URLs (avoids an extra HTTP call per team)
            var homeEspnId = ParseTeamIdFromUrl(homeCompetitor.Team.GetUrl());
            var awayEspnId = ParseTeamIdFromUrl(awayCompetitor.Team.GetUrl());

            if (homeEspnId == null || awayEspnId == null)
                return (null, null);

            // Map ESPN team IDs to Supabase team IDs
            var homeTeamId = ESPNTeamMapper.MapEspnIdToSupabaseId(homeEspnId);
            var awayTeamId = ESPNTeamMapper.MapEspnIdToSupabaseId(awayEspnId);

            return (homeTeamId, awayTeamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting team IDs for game {GameId}", game?.Id);
            return (null, null);
        }
    }

    private static string? ParseTeamIdFromUrl(string url)
    {
        // ESPN team URLs: .../teams/{id} or .../teams/{id}?...
        if (string.IsNullOrEmpty(url)) return null;
        var path = url.Split('?')[0].TrimEnd('/');
        var lastSegment = path.Split('/')[^1];
        return string.IsNullOrEmpty(lastSegment) ? null : lastSegment;
    }

    private async Task ExtractBettingInfo(Game game, Models.Supa.Schedule scheduleRecord)
    {
        try
        {
            var competition = game.Competitions?.FirstOrDefault();
            if (competition?.Odds == null)
            {
                _logger.LogDebug("No odds reference found for game {GameId}", game.Id);
                return;
            }

            // Fetch the odds data from the reference URL
            var oddsUrl = competition.Odds.GetUrl();
            if (string.IsNullOrEmpty(oddsUrl))
            {
                _logger.LogDebug("No odds URL available for game {GameId}", game.Id);
                return;
            }

            _logger.LogDebug("Fetching odds data from URL: {OddsUrl} for game {GameId}", oddsUrl, game.Id);
            var odds = await _espnGameSource.GetOddsAsync(oddsUrl);
            if (odds == null)
            {
                _logger.LogDebug("No odds data returned for game {GameId}", game.Id);
                return;
            }

            _logger.LogDebug("Processing odds for game {GameId} from provider {Provider}",
                game.Id, odds.Provider?.Name ?? "Unknown");

            // Extract Over/Under - use direct property from ESPN API
            if (odds.OverUnder > 0)
            {
                scheduleRecord.OverUnder = odds.OverUnder;
                _logger.LogDebug("Set Over/Under from OverUnder property: {OverUnder} for game {GameId}", scheduleRecord.OverUnder, game.Id);
            }

            // Extract Point Spread (Betting Line) - use direct property from ESPN API
            if (odds.Spread != 0)
            {
                scheduleRecord.BettingLine = odds.Spread;
                _logger.LogDebug("Set Betting Line from Spread property: {BettingLine} for game {GameId}", scheduleRecord.BettingLine, game.Id);
            }

            // Extract Implied Points from Team Odds
            var homeTeamOdds = odds.HomeTeamOdds;
            var awayTeamOdds = odds.AwayTeamOdds;

            // Calculate implied points from spread and over/under (correct method)
            if (scheduleRecord.OverUnder.HasValue && scheduleRecord.BettingLine.HasValue)
            {
                var overUnder = scheduleRecord.OverUnder.Value;
                var spread = scheduleRecord.BettingLine.Value;

                // Home Implied Points = (Over/Under - Point Spread) / 2
                // Away Implied Points = (Over/Under + Point Spread) / 2
                scheduleRecord.HomeImpliedPoints = Math.Round((overUnder - spread) / 2, 1);
                scheduleRecord.AwayImpliedPoints = Math.Round((overUnder + spread) / 2, 1);

                _logger.LogDebug("Calculated implied points for game {GameId}: O/U={OverUnder}, Spread={Spread} → Home={Home}, Away={Away}",
                    game.Id, overUnder, spread, scheduleRecord.HomeImpliedPoints, scheduleRecord.AwayImpliedPoints);
            }
            _logger.LogInformation("✅ Extracted betting info for game {GameId}: Line={BettingLine}, O/U={OverUnder}, Home={HomePoints}, Away={AwayPoints}",
                game.Id, scheduleRecord.BettingLine, scheduleRecord.OverUnder,
                scheduleRecord.HomeImpliedPoints, scheduleRecord.AwayImpliedPoints);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting betting info for game {GameId}", game.Id);
        }
    }

}