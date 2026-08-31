# ESPNScrape

A .NET 9 service that scrapes NFL data from ESPN's API into Supabase.

It runs as a web app with four scheduled background jobs (Quartz.NET). The web
part exists mostly for health checks and manual job triggers — the real work
happens in the jobs.

## The Jobs

| Job | Runs | What it writes |
|---|---|---|
| `NFLWeeklyJob` | Every 2 hours, and once on startup | `PlayerStats` |
| `NFLScheduleSyncJob` | Every hour | `Schedule` |
| `NFLPlayerSyncJob` | Daily at 4:00 AM | `Players.espn_player_id` |
| `NFLPlayerHeadshotJob` | Sundays at 3:00 AM | `Players` headshot fields + image storage |

### NFLWeeklyJob

The main one. Pulls per-player box score stats and saves them.

It asks ESPN which season and week the league is currently in, then scans that
week and the one before it — so games that finalise late still get a second pass.
For each game it walks ESPN's box score and pulls passing, rushing, and receiving
lines for every player who recorded a stat. Outside the regular season it does
nothing. Each line is
matched to a player in our database — first by ESPN ID, then by name within the
team, then against the free-agent bucket. Matched or not, the stat row is saved;
unmatched rows just have a null `player_id`.

### NFLScheduleSyncJob

Keeps the `Schedule` table current: which teams play when, plus betting data
(spread, over/under, and implied points for each team) pulled from ESPN's odds.

**This job feeds the one above.** `PlayerStats` has a foreign key on
`espn_game_id` pointing at `Schedule`, so a game has to exist here before its
stats can be saved.

### NFLPlayerSyncJob

Fills in missing ESPN player IDs on players we already have. It reads all 32
team rosters and, when it can confidently match an ESPN player to one of ours,
writes the ESPN ID onto that record.

It only updates existing players — it never creates new ones.

### NFLPlayerHeadshotJob

Downloads player headshots from ESPN, resizes each into three sizes (full,
profile, thumbnail), uploads them to the Supabase `images` bucket, and saves the
URLs back onto the player record.

Skips any player whose headshot was updated in the last 7 days.

## Running It

```bash
dotnet run
```

Starts the web server on port 8080 and all four jobs on their schedules.

Building requires access to the private BoomBustFantasy NuGet feed (for the
`BoomBust.Logging` and `BoomBust.HealthChecks` packages). Copy
`nuget.config.template` to `nuget.config` and add a GitHub token with
`read:packages`.

Or run the prebuilt image, which needs no NuGet access:

```bash
docker compose up
```

Configuration comes from environment variables (see `docker-compose.yml`) —
`Supabase__Url`, `Supabase__ServiceRoleKey`, and the BetterStack logging values.

## Manual Backfill

To load a past season, trigger the jobs by hand instead of waiting for their
schedule. Run the schedule backfill **first** — stats for a game can't be saved
until that game exists in `Schedule`.

```bash
curl -X POST "http://localhost:8080/api/espn/schedule-backfill/2023?startWeek=1&endWeek=18"
```

Then, once it finishes:

```bash
curl -X POST "http://localhost:8080/api/espn/backfill/2023?startWeek=1&endWeek=18"
```

Both return immediately and run in the background. Watch the logs for progress —
a full season takes a few hours.

## Other Endpoints

| Endpoint | What it does |
|---|---|
| `GET /health` | Detailed health check (JSON) |
| `GET /health/live` | Liveness probe |
| `GET /health/ready` | Readiness probe — checks Supabase and the ESPN API |
| `GET /api/espn/teams/{season}` | NFL teams, straight from ESPN |
| `GET /api/espn/schedule/{season}/{week}` | A week's games, straight from ESPN |
| `GET /api/espn/status` | Job list |

## Layout

```
Program.cs      Startup, DI, and the job schedules
Jobs/           The four Quartz jobs
Services/       ESPN API clients and parsing
  Repositories/ Supabase reads and writes
Models/         ESPN API response shapes
  Supa/         Supabase table models
```
