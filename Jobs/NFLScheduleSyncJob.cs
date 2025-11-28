using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
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
    private readonly ESPNDataService _espnDataService;
    private readonly SupabaseService _supabaseService;

    public NFLScheduleSyncJob(
        ILogger<NFLScheduleSyncJob> logger,
        ESPNDataService espnDataService,
        SupabaseService supabaseService)
    {
        _logger = logger;
        _espnDataService = espnDataService;
        _supabaseService = supabaseService;
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
            // Check for job data parameters (used for specific season/week ranges)
            var jobDataMap = context.MergedJobDataMap;
            int currentSeason;
            List<int> weeksToSync;
            List<int> seasonTypesToSync;

            if (jobDataMap.ContainsKey("season"))
            {
                // Use explicit season and week range from job data
                currentSeason = jobDataMap.GetInt("season");
                var startWeek = jobDataMap.ContainsKey("startWeek") ? jobDataMap.GetInt("startWeek") : 1;
                var endWeek = jobDataMap.ContainsKey("endWeek") ? jobDataMap.GetInt("endWeek") : 18;
                var seasonType = jobDataMap.ContainsKey("seasonType") ? jobDataMap.GetInt("seasonType") : 2; // Default to regular season

                weeksToSync = Enumerable.Range(startWeek, endWeek - startWeek + 1).ToList();
                seasonTypesToSync = new List<int> { seasonType };

                _logger.LogInformation("Using explicit season {Season}, weeks {StartWeek}-{EndWeek}, season type {SeasonType} from job data",
                    currentSeason, startWeek, endWeek, seasonType);
            }
            else
            {
                // Default: sync current season regular season and playoffs
                currentSeason = await GetCurrentNFLSeason();
                weeksToSync = Enumerable.Range(1, 18).ToList(); // Regular season weeks 1-18
                seasonTypesToSync = new List<int> { 2, 3 }; // Regular season (2) and Playoffs (3)

                _logger.LogInformation("Syncing season {Season} for season types: [{SeasonTypes}], weeks: [{Weeks}]",
                    currentSeason, string.Join(", ", seasonTypesToSync), string.Join(", ", weeksToSync));
            }

            // Process each season type (regular season, playoffs, etc.)
            foreach (var seasonType in seasonTypesToSync)
            {
                var seasonTypeName = GetSeasonTypeName(seasonType);
                _logger.LogInformation("=== PROCESSING {SeasonTypeName} (Type {SeasonType}) ===", seasonTypeName, seasonType);

                // For playoffs, adjust week range
                var actualWeeksToSync = seasonType == 3 ? GetPlayoffWeeks() : weeksToSync;

                foreach (var week in actualWeeksToSync)
                {
                    _logger.LogInformation("Processing {SeasonTypeName} Week {Week}", seasonTypeName, week);

                    try
                    {
                        // Get games for this season/week/type
                        var games = await _espnDataService.GetWeeklyGamesAsync(currentSeason, seasonType, week);

                        if (games == null || !games.Any())
                        {
                            _logger.LogInformation("No games found for {SeasonTypeName} {Season} Week {Week}",
                                seasonTypeName, currentSeason, week);
                            continue;
                        }

                        _logger.LogInformation("Found {GameCount} games for {SeasonTypeName} {Season} Week {Week}",
                            games.Count, seasonTypeName, currentSeason, week);

                        // Process each game
                        foreach (var game in games)
                        {
                            var (created, updated, error) = await ProcessGameSchedule(game, currentSeason, seasonType, week);

                            totalGamesProcessed++;
                            if (created) totalGamesCreated++;
                            if (updated) totalGamesUpdated++;
                            if (error) totalErrors++;
                        }
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        _logger.LogError(ex, "Error processing {SeasonTypeName} Week {Week}", seasonTypeName, week);
                    }

                    // Delay between weeks to be respectful to ESPN's servers
                    await Task.Delay(500);
                }

                _logger.LogInformation("=== COMPLETED {SeasonTypeName} ===", seasonTypeName);
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
            var existingSchedule = await _supabaseService.GetScheduleByEspnGameIdAsync(game.Id);

            // Extract team information
            var (homeTeamId, awayTeamId) = await ExtractTeamIds(game);

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
                var success = await _supabaseService.CreateScheduleAsync(scheduleRecord);
                if (success)
                {
                    created = true;
                    _logger.LogInformation("✅ Created schedule record for game {GameId}: {AwayTeam} @ {HomeTeam}",
                        game.Id, await GetTeamName(awayTeamId.Value), await GetTeamName(homeTeamId.Value));
                }
                else
                {
                    // If create failed, try to get the existing record and update it
                    _logger.LogDebug("Create failed for game {GameId}, attempting to update existing record", game.Id);
                    var existingRecord = await _supabaseService.GetScheduleByEspnGameIdAsync(game.Id);
                    if (existingRecord != null)
                    {
                        // Copy the ID and update
                        scheduleRecord.Id = existingRecord.Id;
                        var updateSuccess = await _supabaseService.UpdateScheduleAsync(scheduleRecord);
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
                var success = await _supabaseService.UpdateScheduleAsync(scheduleRecord);
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

    private async Task<(long? homeTeamId, long? awayTeamId)> ExtractTeamIds(Game game)
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

            // Get team data from ESPN references
            var homeTeamData = await GetTeamFromReference(homeCompetitor.Team);
            var awayTeamData = await GetTeamFromReference(awayCompetitor.Team);

            if (homeTeamData?.Id == null || awayTeamData?.Id == null)
                return (null, null);

            // Map ESPN team IDs to Supabase team IDs
            var homeTeamId = ESPNTeamMapper.MapEspnIdToSupabaseId(homeTeamData.Id);
            var awayTeamId = ESPNTeamMapper.MapEspnIdToSupabaseId(awayTeamData.Id);

            return (homeTeamId, awayTeamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting team IDs for game {GameId}", game?.Id);
            return (null, null);
        }
    }

    private async Task<Models.Team?> GetTeamFromReference(TeamReference teamReference)
    {
        try
        {
            if (string.IsNullOrEmpty(teamReference.GetUrl()))
                return null;

            var teamData = await _espnDataService.GetTeamFromUrlAsync(teamReference.GetUrl());
            return teamData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch team from reference: {TeamRef}", teamReference.GetUrl());
            return null;
        }
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
            var odds = await _espnDataService.GetOddsAsync(oddsUrl);
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
            }            _logger.LogInformation("✅ Extracted betting info for game {GameId}: Line={BettingLine}, O/U={OverUnder}, Home={HomePoints}, Away={AwayPoints}",
                game.Id, scheduleRecord.BettingLine, scheduleRecord.OverUnder,
                scheduleRecord.HomeImpliedPoints, scheduleRecord.AwayImpliedPoints);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting betting info for game {GameId}", game.Id);
        }
    }



    private async Task<string> GetTeamName(long teamId)
    {
        try
        {
            var team = await _supabaseService.GetTeamByIdAsync(teamId);
            return team?.Abbreviation ?? $"Team {teamId}";
        }
        catch
        {
            return $"Team {teamId}";
        }
    }

    private async Task<int> GetCurrentNFLSeason()
    {
        try
        {
            var currentDate = DateTime.Now;
            var currentYear = currentDate.Year;

            // NFL season runs from September to February of next year
            // If we're in January-July, the NFL season year is the previous year
            if (currentDate.Month <= 7)
            {
                return currentYear - 1;
            }

            return currentYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining current NFL season, defaulting to 2025");
            return 2025;
        }
    }

    private static string GetSeasonTypeName(int seasonType) => seasonType switch
    {
        1 => "Preseason",
        2 => "Regular Season",
        3 => "Playoffs",
        _ => $"Season Type {seasonType}"
    };

    private static List<int> GetPlayoffWeeks()
    {
        // NFL Playoffs typically have weeks 19-22 (Wild Card, Divisional, Conference, Super Bowl)
        return new List<int> { 19, 20, 21, 22 };
    }
}