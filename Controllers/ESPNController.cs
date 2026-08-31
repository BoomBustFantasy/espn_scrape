using ESPNScrape.Services;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace ESPNScrape.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ESPNController : ControllerBase
{
    private readonly IESPNGameSource _gameSource;
    private readonly IESPNRosterSource _rosterSource;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<ESPNController> _logger;

    public ESPNController(
        IESPNGameSource gameSource,
        IESPNRosterSource rosterSource,
        ISchedulerFactory schedulerFactory,
        ILogger<ESPNController> logger)
    {
        _gameSource = gameSource;
        _rosterSource = rosterSource;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Manually triggers NFLWeeklyJob for a historical season/week range.
    /// Runs against the existing job (same box-score parsing and upsert logic as the live cron job).
    /// </summary>
    [HttpPost("backfill/{season}")]
    public async Task<ActionResult> TriggerHistoricalBackfill(int season, [FromQuery] int startWeek = 1, [FromQuery] int endWeek = 18)
    {
        _logger.LogInformation("Manually triggering NFLWeeklyJob backfill for season {Season}, weeks {StartWeek}-{EndWeek}",
            season, startWeek, endWeek);

        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey("NFLWeeklyJob");

        var dataMap = new JobDataMap
        {
            { "season", season },
            { "startWeek", startWeek },
            { "endWeek", endWeek }
        };

        await scheduler.TriggerJob(jobKey, dataMap);

        return Ok(new
        {
            success = true,
            message = $"Triggered NFLWeeklyJob for season {season}, weeks {startWeek}-{endWeek}. Check logs for progress."
        });
    }

    /// <summary>
    /// Manually triggers NFLScheduleSyncJob for a historical season/week range.
    /// PlayerStats has a FK constraint on espn_game_id referencing Schedule, so this
    /// must be run for a season before NFLWeeklyJob's stats will actually insert.
    /// </summary>
    [HttpPost("schedule-backfill/{season}")]
    public async Task<ActionResult> TriggerScheduleBackfill(int season, [FromQuery] int startWeek = 1, [FromQuery] int endWeek = 18)
    {
        _logger.LogInformation("Manually triggering NFLScheduleSyncJob backfill for season {Season}, weeks {StartWeek}-{EndWeek}",
            season, startWeek, endWeek);

        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey("NFLScheduleSyncJob");

        var dataMap = new JobDataMap
        {
            { "season", season },
            { "startWeek", startWeek },
            { "endWeek", endWeek }
        };

        await scheduler.TriggerJob(jobKey, dataMap);

        return Ok(new
        {
            success = true,
            message = $"Triggered NFLScheduleSyncJob for season {season}, weeks {startWeek}-{endWeek}. Check logs for progress."
        });
    }

    /// <summary>
    /// Get current NFL teams from ESPN API
    /// </summary>
    [HttpGet("teams/{season}")]
    public async Task<ActionResult> GetTeams(int season = 2025)
    {
        try
        {
            _logger.LogInformation("Fetching NFL teams from ESPN API for season {Season}", season);
            var teams = await _rosterSource.GetNFLTeamsAsync(season);
            return Ok(new { success = true, count = teams.Count, teams });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching teams");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get schedule for a specific week
    /// </summary>
    [HttpGet("schedule/{season}/{week}")]
    public async Task<ActionResult> GetSchedule(int season, int week)
    {
        try
        {
            _logger.LogInformation("Fetching schedule for {Season} week {Week}", season, week);
            var games = await _gameSource.GetNFLWeekGamesAsync(season, week);
            return Ok(new { success = true, season, week, count = games.Count, games });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schedule");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get job status information
    /// </summary>
    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            success = true,
            service = "ESPNScrape",
            timestamp = DateTime.UtcNow,
            jobs = new[]
            {
                new { name = "NFLWeeklyJob", schedule = "Every Tuesday at 6:00 AM", cron = "0 0 6 ? * TUE" },
                new { name = "NFLScheduleSyncJob", schedule = "Daily at 5:00 AM", cron = "0 0 5 * * ?" },
                new { name = "NFLPlayerSyncJob", schedule = "Daily at 4:00 AM", cron = "0 0 4 * * ?" },
                new { name = "NFLPlayerHeadshotJob", schedule = "Every Sunday at 3:00 AM", cron = "0 0 3 ? * SUN" }
            }
        });
    }
}
