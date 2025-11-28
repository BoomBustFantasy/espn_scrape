# ESPNScrape Project Instructions

.NET 10 console app using Quartz.NET for scheduled ESPN NFL data scraping to Supabase.

## Architecture Overview

```
Program.cs           → CLI entry with --flags for different jobs
Jobs/                → Quartz jobs (NFLWeeklyJob, NFLScheduleSyncJob, NFLPlayerSyncJob, NFLPlayerHeadshotJob)
Services/            → ESPNDataService (API), SupabaseService (DB), ESPNPlayerMappingService
Models/              → ESPN API response models
Models/Supa/         → Supabase table models (use Postgrest attributes)
Converters/          → ESPNNumericConverter for handling ESPN's inconsistent data types
```

## Running Jobs

```powershell
dotnet run                    # Default: headshot sync job
dotnet run --stats-2025       # Sync player stats for 2025 season (weeks 1-18)
dotnet run --sync-players     # Sync ESPN player IDs to database
dotnet run --sync-schedule    # Sync schedule data (weeks 13-18)
dotnet run --headshots-only   # Sync player headshots
```

## ESPN API Patterns (Critical)

### Two-Step Fetch Required
ESPN collection endpoints return `$ref` references, not data. Always follow refs:
```csharp
// Step 1: Get references from /teams, /events, etc.
var apiResponse = JsonSerializer.Deserialize<ESPNReferenceResponse>(response);
// Step 2: Fetch each item via reference.GetUrl()
foreach (var ref in apiResponse.Items)
    var item = await _httpClient.GetStringAsync(ref.GetUrl());
```

### Reference Models Pattern
See `Models/ESPNReferences.cs` for typed reference classes:
```csharp
public class TeamReference {
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
    public string GetUrl() => Ref;
}
```

### Rate Limiting
**Always add `await Task.Delay(100)` between ESPN API calls** - see `ESPNDataService.cs` for examples.

## ESPN Data Type Gotchas

ESPN returns inconsistent types that break deserialization:

| Field | Possible Values |
|-------|----------------|
| Numeric stats | `15.5`, `"-"`, `"N/A"`, `""`, `"66.7%"` |
| Composite stats | `"12/20"`, `"3-8"`, `"28:45"` |

**Solution**: Use `ESPNNumericConverter` (in `Converters/`) for numeric fields:
```csharp
[JsonConverter(typeof(ESPNNumericConverter))]
public double Value { get; set; }
```

For composite stats, parse from `displayValue` string, not `value` field.

## Supabase Models

Models in `Models/Supa/` map to database tables using Postgrest attributes:
```csharp
[Table("Players")]
public class Player : BaseModel {
    [PrimaryKey("id")][Column("id")] public long Id { get; set; }
    [Column("espn_player_id")] public string? EspnPlayerId { get; set; }
}
```

**Before modifying Supa models**: Query actual schema via MCP Supabase tools:
- Project ID: `afxewbjvvrucstbtzglh`
- Use `mcp_supabase_execute_sql` or `mcp_supabase_list_tables` to verify columns

## Key ESPN Endpoints

| Endpoint | Use |
|----------|-----|
| `sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/{year}/teams/{id}` | Team details |
| `.../seasons/{year}/types/{type}/weeks/{week}/events` | Weekly games (type: 1=pre, 2=reg, 3=post) |
| `site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={id}` | Game summary with boxscore |
| `.../athletes/{id}` | Player details |

## Player Stats Processing

Game stats come from `/summary?event={id}` in `boxScore.players[]`:
- Stats are string arrays indexed by `labels[]`/`keys[]` arrays
- Filter categories: `passing`, `rushing`, `receiving`, `interceptions`, `fumbles`
- Use `displayOrder` (1=away, 2=home) to identify teams

## JSON Serialization

Always use these options:
```csharp
var jsonOptions = new JsonSerializerOptions {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

## Logging

Uses Serilog with console + rolling file output to `logs/`. Jobs log summaries with emoji prefixes:
- `🏆` for weekly job summaries
- `🗓️` for schedule sync
- `✅`/`❌` for completion status