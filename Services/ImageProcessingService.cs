using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using Microsoft.Extensions.Logging;
using ESPNScrape.Models;

namespace ESPNScrape.Services;

/// <summary>
/// Service for processing and resizing player headshot images
/// Creates multiple sizes from a single source image when ESPN doesn't provide all sizes
/// </summary>
public class ImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates multiple headshot sizes from a single source image
    /// </summary>
    /// <param name="sourceImageData">The source image data (typically ESPN's "full" size)</param>
    /// <param name="playerName">Player name for logging</param>
    /// <returns>Dictionary with size name and resized image data</returns>
    public async Task<Dictionary<string, byte[]>> CreateMultipleSizesFromSource(byte[] sourceImageData, string playerName)
    {
        try
        {
            _logger.LogDebug("🎨 Creating multiple sizes from source image for {PlayerName} ({SourceSize} bytes)",
                playerName, sourceImageData.Length);

            var results = new Dictionary<string, byte[]>();

            using var image = SixLabors.ImageSharp.Image.Load(sourceImageData);

            // Original dimensions
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            _logger.LogDebug("📐 Source image dimensions: {Width}x{Height}", originalWidth, originalHeight);

            // Create each size
            foreach (var sizeConfig in ESPNHeadshotSizes.SizeExpectations)
            {
                var sizeName = sizeConfig.Key;
                var targetWidth = sizeConfig.Value.ExpectedWidth;
                var targetHeight = sizeConfig.Value.ExpectedHeight;

                try
                {
                    // Clone the original image for this size
                    using var resizedImage = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();

                    // Resize to target dimensions
                    resizedImage.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Crop, // Crop to maintain aspect ratio and fill target size
                        Position = AnchorPositionMode.Center // Center the crop
                    }));

                    // Convert to byte array
                    using var stream = new MemoryStream();
                    await resizedImage.SaveAsync(stream, new PngEncoder());
                    var resizedData = stream.ToArray();

                    results[sizeName] = resizedData;

                    _logger.LogDebug("✅ Created {SizeName} size: {Width}x{Height} ({FileSize} bytes)",
                        sizeName, targetWidth, targetHeight, resizedData.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "❌ Failed to create {SizeName} size for {PlayerName}", sizeName, playerName);
                }
            }

            _logger.LogDebug("🎨 Created {SizeCount} sizes for {PlayerName}", results.Count, playerName);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating multiple sizes for {PlayerName}", playerName);
            return new Dictionary<string, byte[]>();
        }
    }

    /// <summary>
    /// Validates that image data is a valid image format
    /// </summary>
    /// <param name="imageData">Image data to validate</param>
    /// <returns>True if valid image, false otherwise</returns>
    public bool IsValidImage(byte[] imageData)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(imageData);
            return image.Width > 0 && image.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets image dimensions without fully loading the image
    /// </summary>
    /// <param name="imageData">Image data to analyze</param>
    /// <returns>Tuple with width and height, or null if invalid</returns>
    public (int Width, int Height)? GetImageDimensions(byte[] imageData)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(imageData);
            return (image.Width, image.Height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Optimizes image quality and file size while maintaining visual quality
    /// </summary>
    /// <param name="sourceImageData">Source image data</param>
    /// <param name="maxFileSizeBytes">Maximum file size in bytes (optional)</param>
    /// <returns>Optimized image data</returns>
    public async Task<byte[]> OptimizeImage(byte[] sourceImageData, int? maxFileSizeBytes = null)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(sourceImageData);
            using var stream = new MemoryStream();

            // Use PNG with compression for good quality/size balance
            var encoder = new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            };

            await image.SaveAsync(stream, encoder);
            var optimizedData = stream.ToArray();

            // If max file size specified and we're over, try reducing quality
            if (maxFileSizeBytes.HasValue && optimizedData.Length > maxFileSizeBytes.Value)
            {
                _logger.LogDebug("🗜️ Image size {CurrentSize} exceeds limit {MaxSize}, optimizing further",
                    optimizedData.Length, maxFileSizeBytes.Value);

                // For PNG, we can't reduce quality like JPEG, but we can resize slightly
                var reductionFactor = Math.Sqrt((double)maxFileSizeBytes.Value / optimizedData.Length);
                var newWidth = (int)(image.Width * reductionFactor);
                var newHeight = (int)(image.Height * reductionFactor);

                image.Mutate(x => x.Resize(newWidth, newHeight));

                stream.SetLength(0);
                await image.SaveAsync(stream, encoder);
                optimizedData = stream.ToArray();
            }

            return optimizedData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize image, returning original");
            return sourceImageData;
        }
    }
}