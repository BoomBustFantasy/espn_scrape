namespace ESPNScrape.Services;

public interface IImageStore
{
    Task<(bool Success, string? PublicUrl, string? Error)> UploadImageAsync(string bucketName, string path, byte[] imageData);
}
