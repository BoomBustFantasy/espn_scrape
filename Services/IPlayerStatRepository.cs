using ESPNScrape.Models.Supa;

namespace ESPNScrape.Services;

public interface IPlayerStatRepository
{
    Task<int> UpsertPlayerStatsBatchAsync(IEnumerable<PlayerStat> playerStats);
}
