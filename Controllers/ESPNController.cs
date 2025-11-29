using ESPNScrape.Services;
using Microsoft.AspNetCore.Mvc;

namespace ESPNScrape.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ESPNController : ControllerBase
{
    private readonly IESPNDataService _espnService;
    private readonly ISupabaseService _supabaseService;
    private readonly ILogger<ESPNController> _logger;

    public ESPNController(
        IESPNDataService espnService,
        ISupabaseService supabaseService,
        ILogger<ESPNController> logger)
    {
        _espnService = espnService;
        _supabaseService = supabaseService;
        _logger = logger;
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
            var teams = await _espnService.GetNFLTeamsAsync(season);
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
    public async Task<ActionResult> GetSchedule(int season, int week, [FromQuery] int seasonType = 2)
    {
        try
        {
            _logger.LogInformation("Fetching schedule for {Season} week {Week} (type {SeasonType})", season, week, seasonType);
            var games = await _espnService.GetWeeklyGamesAsync(season, seasonType, week);
            return Ok(new { success = true, season, week, seasonType, count = games.Count, games });
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
