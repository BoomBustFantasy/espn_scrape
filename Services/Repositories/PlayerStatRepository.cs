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
                        // Postgrest-csharp's Update() (both the instance method and the
                        // table-level .Where().Update() form) silently no-ops for this table
                        // in practice — confirmed the same field values reach the client
                        // correctly but never make it into the request. Delete + re-insert
                        // instead, using only the Insert/Delete paths that are proven reliable.
                        var existingRecord = existing.Models.First();
                        await _supabaseClient
                            .From<PlayerStat>()
                            .Where(x => x.EspnPlayerId == stat.EspnPlayerId && x.EspnGameId == stat.EspnGameId)
                            .Delete();

                        stat.Id = null;
                        stat.CreatedAt = existingRecord.CreatedAt;
                        await _supabaseClient
                            .From<PlayerStat>()
                            .Insert(stat);
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
                    _logger.LogWarning("⚠️ Skipped record due to foreign key constraint violation for ESPN Player ID {EspnPlayerId}, Game {EspnGameId}: {Detail}",
                        stat.EspnPlayerId, stat.EspnGameId, pgEx.Message);
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
