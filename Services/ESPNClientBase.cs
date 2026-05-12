using ESPNScrape.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ESPNScrape.Services;

public abstract class ESPNClientBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected ESPNClientBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches all pages of a paginated ESPN collection endpoint, follows each $ref item,
    /// and returns a flat list of the deserialized results.
    /// </summary>
    protected async Task<List<T>> FetchPagedReferencesAsync<T>(string collectionUrl)
    {
        var results = new List<T>();
        var currentPage = 1;
        var totalPages = 1;

        do
        {
            var pagedUrl = $"{collectionUrl}?page={currentPage}";
            var response = await _httpClient.GetStringAsync(pagedUrl);
            var apiResponse = JsonSerializer.Deserialize<ESPNReferenceResponse>(response, JsonOptions);

            if (apiResponse?.Items == null)
                break;

            if (currentPage == 1)
                totalPages = apiResponse.PageCount > 0 ? apiResponse.PageCount : 1;

            foreach (var refItem in apiResponse.Items)
            {
                try
                {
                    var itemResponse = await _httpClient.GetStringAsync(refItem.GetUrl());
                    var item = JsonSerializer.Deserialize<T>(itemResponse, JsonOptions);
                    if (item != null)
                        results.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch item from reference: {RefUrl}", refItem.GetUrl());
                }

                await Task.Delay(100);
            }

            currentPage++;

        } while (currentPage <= totalPages);

        return results;
    }
}
