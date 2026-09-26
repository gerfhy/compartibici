namespace CompartiBici.Services;

public interface IPieSocketService
{
    string ClusterId { get; }
    string ApiKey { get; }
    string RoomId { get; }
    Task<bool> PublicarEventoAsync(string evento, object data);
}
