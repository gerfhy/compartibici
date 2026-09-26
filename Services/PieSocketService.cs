using System.Text;
using System.Text.Json;

namespace CompartiBici.Services;

public class PieSocketService : IPieSocketService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<PieSocketService> _logger;

    public string ClusterId => _config["PieSocket:ClusterId"] 
        ?? _config["PieSocket__ClusterId"] 
        ?? Environment.GetEnvironmentVariable("PieSocket__ClusterId") 
        ?? "free.blr2";

    public string ApiKey => _config["PieSocket:ApiKey"] 
        ?? _config["PieSocket__ApiKey"] 
        ?? Environment.GetEnvironmentVariable("PieSocket__ApiKey") 
        ?? "";

    public string Secret => _config["PieSocket:Secret"] 
        ?? _config["PieSocket:ApiSecret"] 
        ?? _config["PieSocket__Secret"] 
        ?? Environment.GetEnvironmentVariable("PieSocket__Secret") 
        ?? "";

    public string RoomId => _config["PieSocket:RoomId"] 
        ?? _config["PieSocket__RoomId"] 
        ?? Environment.GetEnvironmentVariable("PieSocket__RoomId") 
        ?? "incidencias_canal";

    public PieSocketService(HttpClient httpClient, IConfiguration config, ILogger<PieSocketService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> PublicarEventoAsync(string evento, object data)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(Secret))
        {
            _logger.LogWarning("PieSocket ApiKey o Secret no están configurados.");
            return false;
        }

        try
        {
            var url = $"https://{ClusterId}.piesocket.com/api/v4/publish";
            var payload = new
            {
                key = ApiKey,
                secret = Secret,
                roomId = RoomId,
                message = new
                {
                    @event = evento,
                    data = data
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, jsonContent);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(">>> [WEBSOCKET PUBLISHED] Evento {Evento} publicado exitosamente en canal {RoomId}", evento, RoomId);
                return true;
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Fallo al publicar evento en PieSocket. Código: {Code}, Detalle: {Body}", response.StatusCode, body);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al publicar evento {Evento} en PieSocket", evento);
            return false;
        }
    }
}
