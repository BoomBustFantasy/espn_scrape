using ESPNScrape.Models.Supa;
using Microsoft.Extensions.Logging;
using Supabase;

namespace ESPNScrape.Services.Repositories;

public class PlayerStatRepository : IPlayerStatRepository
{
    private readonly Client _supabaseClient;
    private readonly ILogger<PlayerStatRepository> _logger;

    public PlayerStatRepository(Client supabaseClient, ILogger<PlayerStatRepository> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<int> UpsertPlayerStatsBatchAsync(IEnumerable<PlayerStat> playerStats)
    {
        if (playerStats == null || !playerStats.Any())
        {
            _logger.LogWarning("UpsertPlayerStatsBatchAsync called with empty collection");
            return 0;
        }

        var statsList = playerStats.ToList();
        var now = DateTime.UtcNow;

        var totalRecords = statsList.Count;
        var updatedCount = 0;
        var insertedCount = 0;
        var failedCount = 0;

        foreach (var stat in statsList)
        {
            if (stat.CreatedAt == default)
                stat.CreatedAt = now;
            stat.UpdatedAt = now;
        }

        try
        {
            foreach (var stat in statsList)
            {
                try
                {
                    var existing = await _supabaseClient
                        .From<PlayerStat>()
                        .Where(x => x.EspnPlayerId == stat.EspnPlayerId && x.EspnGameId == stat.EspnGameId)
                        .Get();

                    if (existing.Models.Any())
                    {
                        var existingRecord = existing.Models.First();
                        existingRecord.Passing = stat.Passing;
                        existingRecord.Rushing = stat.Rushing;
                        existingRecord.Receiving = stat.Receiving;
                        existingRecord.Fumbles = stat.Fumbles;
                        existingRecord.FumblesLost = stat.FumblesLost;
                        existingRecord.UpdatedAt = DateTime.UtcNow;

                        await existingRecord.Update<PlayerStat>();
                        updatedCount++;
                    }
                    else
                    {
                        stat.Id = null;
                        await _supabaseClient
                            .From<PlayerStat>()
                            .Insert(stat);
                        insertedCount++;
                    }
                }
                catch (Supabase.Postgrest.Exceptions.PostgrestException pgEx) when (pgEx.Message.Contains("23503"))
                {
                    failedCount++;
                    _logger.LogWarning("⚠️ Skipped record due to foreign key constraint: ESPN Player ID {EspnPlayerId} not found in Players table. Game: {EspnGameId}",
                        stat.EspnPlayerId, stat.EspnGameId);
                }
                catch (Exception individualEx)
                {
                    failedCount++;
                    _logger.LogError(individualEx, "Failed to process record for ESPN Player ID: {EspnPlayerId}, Game ID: {EspnGameId}",
                        stat.EspnPlayerId, stat.EspnGameId);
                }

                await Task.Delay(50);
            }

            _logger.LogInformation("📊 Database Upsert Summary: {TotalRecords} total records | {UpdatedCount} updated | {InsertedCount} inserted | {FailedCount} failed",
                totalRecords, updatedCount, insertedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch upsert of player stats");
        }

        return updatedCount + insertedCount;
    }
}
