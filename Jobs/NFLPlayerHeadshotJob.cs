using System.Text.Json;
using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ESPNScrape.Jobs;

[DisallowConcurrentExecution]
public class NFLPlayerHeadshotJob : IJob
{
    private readonly ILogger<NFLPlayerHeadshotJob> _logger;
    private readonly IESPNDataService _espnDataService;
    private readonly ISupabaseService _supabaseService;
    private readonly HttpClient _httpClient;
    private readonly ImageProcessingService _imageProcessingService;

    public NFLPlayerHeadshotJob(ILogger<NFLPlayerHeadshotJob> logger, IESPNDataService espnDataService,
        ISupabaseService supabaseService, HttpClient httpClient, ImageProcessingService imageProcessingService)
    {
        _logger = logger;
        _espnDataService = espnDataService;
        _supabaseService = supabaseService;
        _httpClient = httpClient;
        _imageProcessingService = imageProcessingService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Check if we should force refresh
        var forceRefresh = Environment.GetCommandLineArgs().Contains("--force-refresh");

        _logger.LogInformation("🖼️ Starting NFL Player Headshot scraping job{ForceRefresh}",
            forceRefresh ? " (FORCE REFRESH - Ignoring recent updates)" : "");

        // Track overall job statistics
        var totalPlayersProcessed = 0;
        var totalHeadshotsDownloaded = 0;
        var totalPlayersUpdated = 0;
        var totalErrors = 0;

        try
        {
            // Get current NFL season
            var currentSeason = await GetCurrentNFLSeason();
            _logger.LogInformation("Processing headshots for NFL {Season} season", currentSeason);

            // Get all NFL teams for the current season
            var teams = await _espnDataService.GetNFLTeamsAsync(currentSeason);

            if (teams == null || !teams.Any())
            {
                _logger.LogWarning("No NFL teams found for season {Season}", currentSeason);
                return;
            }

            // Process ALL teams (no debug limitations)

            _logger.LogInformation("Found {TeamCount} NFL teams for season {Season}", teams.Count(), currentSeason);

            // Process each team's roster
            foreach (var team in teams)
            {
                _logger.LogInformation("=== PROCESSING TEAM: {TeamName} (ID: {TeamId}) ===",
                    team.DisplayName, team.Id);

                try
                {
                    // Get team roster from ESPN
                    var roster = await _espnDataService.GetTeamRosterAsync(currentSeason, team.Id);

                    if (roster == null || !roster.Any())
                    {
                        _logger.LogInformation("No roster found for {TeamName}", team.DisplayName);
                        continue;
                    }

                    // Process ALL players on the roster (no debug limitations)

                    _logger.LogInformation("Found {PlayerCount} players on {TeamName} roster",
                        roster.Count(), team.DisplayName);

                    // Process each player on the roster
                    foreach (var espnPlayer in roster)
                    {
                        totalPlayersProcessed++;

                        try
                        {
                            var result = await ProcessPlayerHeadshot(espnPlayer, team.DisplayName, currentSeason);

                            if (result.Downloaded)
                            {
                                totalHeadshotsDownloaded++;
                            }

                            if (result.Updated)
                            {
                                totalPlayersUpdated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            totalErrors++;
                            _logger.LogError(ex, "Error processing headshot for player {PlayerName} (ESPN ID: {EspnId})",
                                espnPlayer.DisplayName, espnPlayer.Id);
                        }

                        // Rate limiting - be respectful to ESPN's servers and Supabase Storage
                        await Task.Delay(200);
                    }

                    _logger.LogInformation("=== COMPLETED TEAM: {TeamName} ===", team.DisplayName);
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    _logger.LogError(ex, "Error processing team {TeamName}", team.DisplayName);
                }

                // Delay between teams to be respectful
                await Task.Delay(1000);
            }

            // Log overall job summary
            _logger.LogInformation("🖼️ NFL PLAYER HEADSHOT JOB SUMMARY: {TotalPlayers} players processed | {TotalDownloaded} headshots downloaded | {TotalUpdated} players updated | {TotalErrors} errors",
                totalPlayersProcessed, totalHeadshotsDownloaded, totalPlayersUpdated, totalErrors);
            _logger.LogInformation("✅ Completed NFL Player Headshot scraping job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Fatal error occurred while scraping NFL player headshots");
            throw;
        }
    }

    private async Task<(bool Downloaded, bool Updated)> ProcessPlayerHeadshot(Models.Player espnPlayer, string teamName, int season)
    {
        var playerName = espnPlayer.DisplayName ?? "Unknown Player";
        var espnPlayerId = espnPlayer.Id ?? "";

        if (string.IsNullOrEmpty(espnPlayerId))
        {
            _logger.LogWarning("⚠️ Player {PlayerName} has no ESPN ID, skipping", playerName);
            return (false, false);
        }

        // Check if player has a headshot URL
        var headshotUrl = espnPlayer.Headshot?.Href;
        if (string.IsNullOrEmpty(headshotUrl))
        {
            _logger.LogDebug("📷 No headshot URL found for {PlayerName} (ESPN ID: {EspnId})",
                playerName, espnPlayerId);
            return (false, false);
        }

        _logger.LogInformation("🔍 Processing multi-size headshots for {PlayerName} (ESPN ID: {EspnId})",
            playerName, espnPlayerId);

        try
        {
            // Check if we should force refresh or skip recent headshots
            var forceRefresh = Environment.GetCommandLineArgs().Contains("--force-refresh");

            if (!forceRefresh)
            {
                // Check if we already have recent headshots (avoid re-downloading)
                var existingPlayer = await _supabaseService.GetPlayerByEspnIdAsync(espnPlayerId);

                if (existingPlayer != null && !string.IsNullOrEmpty(existingPlayer.HeadshotUrl) &&
                    existingPlayer.HeadshotUpdatedAt.HasValue &&
                    existingPlayer.HeadshotUpdatedAt.Value > DateTime.UtcNow.AddDays(-7)) // Only update if older than 7 days
                {
                    _logger.LogDebug("⏭️ Headshots for {PlayerName} are recent, skipping", playerName);
                    return (false, false);
                }
            }
            else
            {
                _logger.LogDebug("🔄 Force refresh enabled - processing {PlayerName} regardless of recent updates", playerName);
            }            // Process multiple headshot sizes
            var headshotSizes = await ProcessMultipleHeadshotSizes(headshotUrl, playerName, espnPlayerId, teamName, season);

            if (headshotSizes == null || (headshotSizes.Full == null && headshotSizes.Profile == null && headshotSizes.Thumbnail == null))
            {
                _logger.LogWarning("❌ Failed to download any headshot sizes for {PlayerName}", playerName);
                return (false, false);
            }

            // Update player record in database with multiple sizes
            var updateResult = await UpdatePlayerMultiSizeHeadshotInfo(espnPlayerId, playerName, teamName, headshotSizes, espnPlayer.Headshot);

            if (updateResult)
            {
                var downloadedSizes = GetDownloadedSizesList(headshotSizes);
                _logger.LogInformation("✅ Successfully processed multi-size headshots for {PlayerName} - Sizes: [{Sizes}]",
                    playerName, string.Join(", ", downloadedSizes));
                return (true, true);
            }
            else
            {
                // Player not in database - this is normal and already logged as info in UpdatePlayerMultiSizeHeadshotInfo
                return (false, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing headshots for {PlayerName} (ESPN ID: {EspnId})",
                playerName, espnPlayerId);
            return (false, false);
        }
    }

    private async Task<byte[]?> DownloadImage(string imageUrl)
    {
        try
        {
            _logger.LogDebug("📥 Downloading image from {Url}", imageUrl);

            var response = await _httpClient.GetAsync(imageUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} when downloading image from {Url}",
                    response.StatusCode, imageUrl);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!IsValidImageContentType(contentType))
            {
                _logger.LogWarning("Invalid content type {ContentType} for image from {Url}",
                    contentType, imageUrl);
                return null;
            }

            var imageData = await response.Content.ReadAsByteArrayAsync();
            _logger.LogDebug("✅ Downloaded image - Size: {Size} bytes, Type: {ContentType}",
                imageData.Length, contentType);

            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image from {Url}", imageUrl);
            return null;
        }
    }

    private async Task<(bool Success, string? PublicUrl, string? Error)> UploadToSupabaseStorage(string storagePath, byte[] imageData)
    {
        try
        {
            _logger.LogDebug("☁️ Uploading to Supabase Storage: {Path}", storagePath);

            // Use the SupabaseService to upload the image
            var result = await _supabaseService.UploadImageAsync("images", storagePath, imageData);

            if (result.Success)
            {
                _logger.LogDebug("✅ Successfully uploaded to storage: {Path}", storagePath);
                return (true, result.PublicUrl, null);
            }
            else
            {
                _logger.LogError("❌ Failed to upload to storage: {Error}", result.Error);
                return (false, null, result.Error);
            }
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            _logger.LogError(ex, "Error uploading to Supabase Storage: {Path}", storagePath);
            return (false, null, error);
        }
    }

    private async Task<PlayerHeadshotSizes?> ProcessMultipleHeadshotSizes(string baseHeadshotUrl, string playerName, string espnPlayerId, string teamName, int season)
    {
        try
        {
            var headshotSizes = new PlayerHeadshotSizes();

            // Download the single source image from ESPN (use the provided URL directly)
            _logger.LogDebug("📥 Downloading source headshot from {Url}", baseHeadshotUrl);
            var sourceImageData = await DownloadImage(baseHeadshotUrl);

            if (sourceImageData == null || sourceImageData.Length == 0)
            {
                _logger.LogWarning("❌ Failed to download source headshot for {PlayerName} from {Url}", playerName, baseHeadshotUrl);
                return null;
            }

            _logger.LogInformation("🎨 Downloaded source image for {PlayerName}, generating multiple sizes", playerName);

            // Generate all sizes from the source image
            var generatedSizes = await _imageProcessingService.CreateMultipleSizesFromSource(sourceImageData, playerName);

            if (generatedSizes == null || !generatedSizes.Any())
            {
                _logger.LogWarning("❌ Failed to generate any sizes for {PlayerName}", playerName);
                return null;
            }

            // Process each generated size
            foreach (var sizeType in ESPNHeadshotSizes.AllSizes)
            {
                if (!generatedSizes.ContainsKey(sizeType))
                {
                    _logger.LogWarning("❌ No {SizeType} image generated for {PlayerName}", sizeType, playerName);
                    continue;
                }

                byte[] imageData = generatedSizes[sizeType];

                // Generate storage path for this size
                var storagePath = $"player-headshots/{season}/{SanitizeFileName(teamName)}/{sizeType}/{SanitizeFileName(playerName)}_{espnPlayerId}.png";

                // Upload to Supabase Storage
                var storageResult = await UploadToSupabaseStorage(storagePath, imageData);
                if (storageResult.Success)
                {
                    var headshotSize = new HeadshotSize
                    {
                        Url = storageResult.PublicUrl ?? "",
                        StoragePath = storagePath,
                        Width = ESPNHeadshotSizes.SizeExpectations.GetValueOrDefault(sizeType).ExpectedWidth,
                        Height = ESPNHeadshotSizes.SizeExpectations.GetValueOrDefault(sizeType).ExpectedHeight,
                        FileSize = imageData.Length,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Assign to appropriate property
                    switch (sizeType)
                    {
                        case ESPNHeadshotSizes.Full:
                            headshotSizes.Full = headshotSize;
                            break;
                        case ESPNHeadshotSizes.Profile:
                            headshotSizes.Profile = headshotSize;
                            break;
                        case ESPNHeadshotSizes.Thumbnail:
                            headshotSizes.Thumbnail = headshotSize;
                            break;
                    }

                    _logger.LogDebug("✅ Successfully uploaded {SizeType} headshot (generated) - Size: {FileSize} bytes",
                        sizeType, imageData.Length);
                }
                else
                {
                    _logger.LogWarning("❌ Failed to upload {SizeType} headshot: {Error}", sizeType, storageResult.Error);
                }
            }

            // Check if we processed any sizes
            var processedCount = (headshotSizes.Full != null ? 1 : 0) +
                               (headshotSizes.Profile != null ? 1 : 0) +
                               (headshotSizes.Thumbnail != null ? 1 : 0);

            if (processedCount > 0)
            {
                _logger.LogInformation("✅ Generated and uploaded {ProcessedCount} headshot sizes for {PlayerName}",
                    processedCount, playerName);
                return headshotSizes;
            }

            _logger.LogWarning("❌ No headshot images could be processed for {PlayerName}", playerName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing multiple headshot sizes for {PlayerName}", playerName);
            return null;
        }
    }

    private async Task<bool> UpdatePlayerMultiSizeHeadshotInfo(string espnPlayerId, string playerName, string teamName,
        PlayerHeadshotSizes headshotSizes, Headshot? headshotInfo)
    {
        try
        {
            // Get or create the player record
            var player = await _supabaseService.GetPlayerByEspnIdAsync(espnPlayerId);
            if (player == null)
            {
                _logger.LogInformation("⏭️ Player {PlayerName} (ESPN ID: {EspnId}) not found in database, skipping (will be created when they appear in game stats)",
                    playerName, espnPlayerId);
                return false;
            }

            // Update headshot information
            var primaryUrl = headshotSizes.Full?.Url ?? headshotSizes.Profile?.Url ?? headshotSizes.Thumbnail?.Url;
            var primaryStoragePath = headshotSizes.Full?.StoragePath ?? headshotSizes.Profile?.StoragePath ?? headshotSizes.Thumbnail?.StoragePath;

            player.HeadshotUrl = primaryUrl;
            player.HeadshotAlt = headshotInfo?.Alt ?? $"{playerName} headshot";
            player.HeadshotWidth = headshotSizes.Full?.Width ?? headshotSizes.Profile?.Width;
            player.HeadshotHeight = headshotSizes.Full?.Height ?? headshotSizes.Profile?.Height;
            player.HeadshotUpdatedAt = DateTime.UtcNow;
            player.StoragePath = primaryStoragePath;
            player.HeadshotSizes = System.Text.Json.JsonSerializer.Serialize(headshotSizes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            player.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _supabaseService.UpdatePlayerAsync(player);
            if (updateResult)
            {
                _logger.LogDebug("✅ Updated player {PlayerName} multi-size headshot info in database", playerName);
                return true;
            }
            else
            {
                _logger.LogError("❌ Failed to update multi-size headshot info for player {PlayerName} in database", playerName);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating multi-size headshot info for player {PlayerName} (ESPN ID: {EspnId})",
                playerName, espnPlayerId);
            return false;
        }
    }

    private static List<string> GetDownloadedSizesList(PlayerHeadshotSizes headshotSizes)
    {
        var sizes = new List<string>();

        if (headshotSizes.Full != null) sizes.Add($"full ({headshotSizes.Full.FileSize} bytes)");
        if (headshotSizes.Profile != null) sizes.Add($"profile ({headshotSizes.Profile.FileSize} bytes)");
        if (headshotSizes.Thumbnail != null) sizes.Add($"thumbnail ({headshotSizes.Thumbnail.FileSize} bytes)");

        return sizes;
    }

    private static string? GetBaseHeadshotUrl(string fullUrl)
    {
        try
        {
            // ESPN URL format: https://a.espncdn.com/i/headshots/nfl/players/full/4361741.png
            // We want: https://a.espncdn.com/i/headshots/nfl/players
            var uri = new Uri(fullUrl);
            var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Find the index of "players" and build base URL up to that point
            var playersIndex = Array.FindIndex(pathParts, p => p.Equals("players", StringComparison.OrdinalIgnoreCase));
            if (playersIndex == -1) return null;

            var basePath = "/" + string.Join("/", pathParts.Take(playersIndex + 1));
            return $"{uri.Scheme}://{uri.Host}{basePath}";
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> UpdatePlayerHeadshotInfo(string espnPlayerId, string playerName, string teamName,
        string? publicUrl, string storagePath, Headshot? headshotInfo, int imageSize)
    {
        try
        {
            // Get or create the player record
            var player = await _supabaseService.GetPlayerByEspnIdAsync(espnPlayerId);
            if (player == null)
            {
                _logger.LogDebug("Player {PlayerName} (ESPN ID: {EspnId}) not found in database, will be created when processing game stats",
                    playerName, espnPlayerId);
                return false;
            }

            // Update headshot information
            player.HeadshotUrl = publicUrl;
            player.HeadshotAlt = headshotInfo?.Alt ?? $"{playerName} headshot";
            player.HeadshotWidth = null; // ESPN doesn't provide width/height in API
            player.HeadshotHeight = null; // ESPN doesn't provide width/height in API
            player.HeadshotUpdatedAt = DateTime.UtcNow;
            player.StoragePath = storagePath;
            player.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _supabaseService.UpdatePlayerAsync(player);
            if (updateResult)
            {
                _logger.LogDebug("✅ Updated player {PlayerName} headshot info in database", playerName);
                return true;
            }
            else
            {
                _logger.LogError("❌ Failed to update headshot info for player {PlayerName} in database", playerName);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating headshot info for player {PlayerName} (ESPN ID: {EspnId})",
                playerName, espnPlayerId);
            return false;
        }
    }

    private async Task<int> GetCurrentNFLSeason()
    {
        try
        {
            var currentDate = DateTime.Now;
            var currentYear = currentDate.Year;

            // NFL season runs from September to February of next year
            // If we're in January-July, the NFL season year is the previous year
            if (currentDate.Month <= 7)
            {
                return currentYear - 1;
            }

            return currentYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining current NFL season, defaulting to 2025");
            return 2025;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove or replace invalid characters for file paths
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Additional cleanup for common problematic characters
        sanitized = sanitized.Replace(' ', '_')
                            .Replace('.', '_')
                            .Replace("'", "")
                            .Replace("\"", "");

        return sanitized;
    }

    private static string? GetFileExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path)?.TrimStart('.');

            // Default to jpg if no extension found or if it's a generic image extension
            if (string.IsNullOrEmpty(extension) || extension.Length > 4)
            {
                return "jpg";
            }

            return extension.ToLower();
        }
        catch
        {
            return "jpg";
        }
    }

    private static bool IsValidImageContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        var validTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        return validTypes.Contains(contentType.ToLower());
    }
}