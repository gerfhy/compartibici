using System.Text.Json;

namespace CompartiBici.Services;

public class AlgoliaSearchService : IAlgoliaSearchService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AlgoliaSearchService> _logger;

    public AlgoliaSearchService(HttpClient httpClient, IConfiguration config, ILogger<AlgoliaSearchService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<List<int>> BuscarIncidenciasAsync(string query)
    {
        var resultIds = new List<int>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return resultIds;
        }

        var appId = _config["Algolia:ApplicationId"] 
            ?? _config["Algolia__ApplicationId"] 
            ?? Environment.GetEnvironmentVariable("Algolia__ApplicationId")
            ?? "YKB1BBI5WH";

        var apiKey = _config["Algolia:SearchApiKey"] 
            ?? _config["Algolia__SearchApiKey"] 
            ?? _config["Algolia:ApiKey"] 
            ?? _config["Algolia__ApiKey"] 
            ?? _config["Algolia:WriteApiKey"]
            ?? Environment.GetEnvironmentVariable("Algolia__SearchApiKey")
            ?? Environment.GetEnvironmentVariable("Algolia__ApiKey");

        var indexName = _config["Algolia:IndexName"] 
            ?? _config["Algolia__IndexName"] 
            ?? Environment.GetEnvironmentVariable("Algolia__IndexName") 
            ?? "incidencias";

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Algolia AppId o ApiKey no configurados correctamente.");
            return resultIds;
        }

        try
        {
            var url = $"https://{appId}-dsn.algolia.net/1/indexes/{indexName}?query={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Algolia-Application-Id", appId);
            request.Headers.Add("X-Algolia-API-Key", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al consultar Algolia: {StatusCode}", response.StatusCode);
                return resultIds;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("hits", out var hitsElement) && hitsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var hit in hitsElement.EnumerateArray())
                {
                    if (hit.TryGetProperty("objectID", out var objIdProp))
                    {
                        if (int.TryParse(objIdProp.GetString(), out var id))
                        {
                            resultIds.Add(id);
                        }
                    }
                    else if (hit.TryGetProperty("id", out var idProp))
                    {
                        if (idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt32(out var idNum))
                        {
                            resultIds.Add(idNum);
                        }
                    }
                }
            }

            _logger.LogInformation("Algolia devolvió {Count} coincidencias para el término '{Query}'", resultIds.Count, query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la búsqueda en Algolia con el término '{Query}'", query);
        }

        return resultIds;
    }
}
