using Microsoft.Extensions.Logging;
using Supabase;

namespace ESPNScrape.Services.Repositories;

public class SupabaseImageStore : IImageStore
{
    private readonly Client _supabaseClient;
    private readonly ILogger<SupabaseImageStore> _logger;

    public SupabaseImageStore(Client supabaseClient, ILogger<SupabaseImageStore> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<(bool Success, string? PublicUrl, string? Error)> UploadImageAsync(string bucketName, string path, byte[] imageData)
    {
        if (string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(path) || imageData == null || imageData.Length == 0)
        {
            return (false, null, "Invalid parameters for image upload");
        }

        try
        {
            _logger.LogDebug("Uploading image to bucket {Bucket}, path {Path}, size {Size} bytes",
                bucketName, path, imageData.Length);

            var uploadResult = await _supabaseClient.Storage
                .From(bucketName)
                .Upload(imageData, path, new Supabase.Storage.FileOptions
                {
                    ContentType = GetContentTypeFromPath(path),
                    Upsert = true
                });

            if (!string.IsNullOrEmpty(uploadResult))
            {
                var publicUrl = _supabaseClient.Storage
                    .From(bucketName)
                    .GetPublicUrl(path);

                _logger.LogDebug("Successfully uploaded image - Public URL: {PublicUrl}", publicUrl);
                return (true, publicUrl, null);
            }
            else
            {
                var error = "Upload failed - no response from storage service";
                _logger.LogError(error);
                return (false, null, error);
            }
        }
        catch (Exception ex)
        {
            var error = $"Error uploading image: {ex.Message}";
            _logger.LogError(ex, "Error uploading image to bucket {Bucket}, path {Path}", bucketName, path);
            return (false, null, error);
        }
    }

    private static string GetContentTypeFromPath(string path)
    {
        var extension = Path.GetExtension(path)?.ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
