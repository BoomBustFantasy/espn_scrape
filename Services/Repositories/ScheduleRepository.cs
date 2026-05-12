using ESPNScrape.Models.Supa;
using Microsoft.Extensions.Logging;
using Supabase;

namespace ESPNScrape.Services.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly Client _supabaseClient;
    private readonly ILogger<ScheduleRepository> _logger;

    public ScheduleRepository(Client supabaseClient, ILogger<ScheduleRepository> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<Schedule?> GetScheduleByEspnGameIdAsync(string espnGameId)
    {
        try
        {
            var result = await _supabaseClient
                .From<Schedule>()
                .Where(s => s.EspnGameId == espnGameId)
                .Get();

            return result.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule by ESPN game ID {EspnGameId}", espnGameId);
            return null;
        }
    }

    public async Task<bool> CreateScheduleAsync(Schedule schedule)
    {
        try
        {
            schedule.CreatedAt = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;

            await _supabaseClient
                .From<Schedule>()
                .Insert(schedule);

            _logger.LogDebug("Successfully created schedule record for game {EspnGameId}", schedule.EspnGameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule record for game {EspnGameId}", schedule.EspnGameId);
            return false;
        }
    }

    public async Task<bool> UpdateScheduleAsync(Schedule schedule)
    {
        try
        {
            schedule.UpdatedAt = DateTime.UtcNow;

            await _supabaseClient
                .From<Schedule>()
                .Where(s => s.Id == schedule.Id)
                .Update(schedule);

            _logger.LogDebug("Successfully updated schedule record {ScheduleId}", schedule.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule record {ScheduleId}", schedule.Id);
            return false;
        }
    }
}
