using ESPNScrape.Models.Supa;

namespace ESPNScrape.Services;

public interface IScheduleRepository
{
    Task<Schedule?> GetScheduleByEspnGameIdAsync(string espnGameId);
    Task<bool> CreateScheduleAsync(Schedule schedule);
    Task<bool> UpdateScheduleAsync(Schedule schedule);
}
