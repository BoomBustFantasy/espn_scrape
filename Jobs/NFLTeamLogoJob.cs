using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using Supabase;

namespace ESPNScrape.Jobs;

[DisallowConcurrentExecution]
public class NFLTeamLogoJob : IJob
{
    private readonly ILogger<NFLTeamLogoJob> _logger;
    private readonly IImageStore _imageStore;
    private readonly HttpClient _httpClient;
    private readonly Client _supabaseClient;

    public NFLTeamLogoJob(ILogger<NFLTeamLogoJob> logger, IImageStore imageStore,
        HttpClient httpClient, Client supabaseClient)
    {
        _logger = logger;
        _imageStore = imageStore;
        _httpClient = httpClient;
        _supabaseClient = supabaseClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("🏈 Starting NFL Team Logo download job");

        var successes = 0;
        var failures = 0;

        try
        {
            var response = await _supabaseClient
                .From<Team>()
                .Select("*")
                .Get();

            var teams = response.Models;

            if (teams == null || teams.Count == 0)
            {
                _logger.LogWarning("No teams found in database");
                return;
            }

            _logger.LogInformation("Found {TeamCount} teams in database", teams.Count);

            foreach (var team in teams)
            {
                // Skip FA (33) and PICK (34)
                if (team.Id == 33 || team.Id == 34)
                    continue;

                try
                {
                    var espnAbbr = ESPNTeamMapper.MapSupabaseAbbreviationToEspn(team.Abbreviation);
                    var cdnUrl = $"https://a.espncdn.com/combiner/i?img=/i/teamlogos/nfl/500/{espnAbbr}.png&h=500&w=500";

                    _logger.LogInformation("Downloading logo for {TeamName} ({Abbr}) from ESPN CDN",
                        team.FullName, team.Abbreviation);

                    var imageData = await _httpClient.GetByteArrayAsync(cdnUrl);

                    var storagePath = $"team-logos/{team.Abbreviation}.png";
                    var (success, publicUrl, error) = await _imageStore.UploadImageAsync("images", storagePath, imageData);

                    if (!success || string.IsNullOrEmpty(publicUrl))
                    {
                        _logger.LogError("❌ Failed to upload logo for {TeamName}: {Error}",
                            team.FullName, error);
                        failures++;
                        continue;
                    }

                    // Update team record with logo URL
                    await _supabaseClient
                        .From<Team>()
                        .Where(t => t.Id == team.Id)
                        .Set(t => t.LogoUrl!, publicUrl)
                        .Update();

                    _logger.LogInformation("✅ Logo uploaded for {TeamName}: {Url}",
                        team.FullName, publicUrl);
                    successes++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing logo for {TeamName} (ID: {TeamId})",
                        team.FullName, team.Id);
                    failures++;
                }

                await Task.Delay(200);
            }

            _logger.LogInformation("🏈 NFL TEAM LOGO JOB SUMMARY: {Successes} succeeded | {Failures} failed",
                successes, failures);
            _logger.LogInformation("✅ Completed NFL Team Logo download job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Fatal error occurred while downloading NFL team logos");
            throw;
        }
    }
}
