using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DGustAR.Api.Services;

public class AppWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _rooms = new();
    private readonly ILogger<AppWebSocketManager> _logger;

    public AppWebSocketManager(ILogger<AppWebSocketManager> logger)
    {
        _logger = logger;
    }

    public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
    {
        var connectionId = Guid.NewGuid().ToString();
        _sockets.TryAdd(connectionId, webSocket);
        _logger.LogInformation("[WS Manager] Cliente conectado: {ConnectionId}", connectionId);

        // Mensaje de bienvenida inicial que espera el frontend
        var welcomeMessage = new
        {
            type = "connected",
            message = "Conexión WebSocket establecida con AR Menu Server (.NET)",
            timestamp = DateTime.UtcNow.ToString("o")
        };
        await SendJsonAsync(webSocket, welcomeMessage);

        var buffer = new byte[1024 * 64];

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cerrado por cliente", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(connectionId, webSocket, messageText);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WS Manager] Error o desconexión en socket {ConnectionId}", connectionId);
        }
        finally
        {
            CleanupSocket(connectionId);
        }
    }

    private async Task ProcessMessageAsync(string connectionId, WebSocket webSocket, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();

            if (type == "subscribe" && root.TryGetProperty("room", out var roomProp))
            {
                var room = roomProp.GetString();
                if (!string.IsNullOrWhiteSpace(room))
                {
                    Subscribe(connectionId, webSocket, room.Trim());
                }
            }
            else if (type == "unsubscribe" && root.TryGetProperty("room", out var unRoomProp))
            {
                var room = unRoomProp.GetString();
                if (!string.IsNullOrWhiteSpace(room))
                {
                    Unsubscribe(connectionId, room.Trim());
                }
            }
            else if (type == "ping")
            {
                await SendJsonAsync(webSocket, new { type = "pong", timestamp = DateTime.UtcNow.ToString("o") });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WS Manager] Error procesando mensaje de cliente: {Json}", json);
        }
    }

    public void Subscribe(string connectionId, WebSocket socket, string room)
    {
        var roomSockets = _rooms.GetOrAdd(room, _ => new ConcurrentDictionary<string, WebSocket>());
        roomSockets[connectionId] = socket;
        _logger.LogInformation("[WS Manager] Conexión {ConnectionId} suscrita a sala '{Room}'", connectionId, room);
    }

    public void Unsubscribe(string connectionId, string room)
    {
        if (_rooms.TryGetValue(room, out var roomSockets))
        {
            roomSockets.TryRemove(connectionId, out _);
        }
    }

    public void CleanupSocket(string connectionId)
    {
        _sockets.TryRemove(connectionId, out _);
        foreach (var room in _rooms.Values)
        {
            room.TryRemove(connectionId, out _);
        }
        _logger.LogInformation("[WS Manager] Limpieza de conexión {ConnectionId} completada", connectionId);
    }

    public async Task BroadcastToRoomAsync(string room, string eventName, object? data)
    {
        if (!_rooms.TryGetValue(room, out var roomSockets) || roomSockets.IsEmpty)
        {
            return;
        }

        var payload = new
        {
            type = "event",
            room,
            @event = eventName,
            data,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var deadSockets = new List<string>();

        foreach (var (connId, socket) in roomSockets)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    deadSockets.Add(connId);
                }
            }
            else
            {
                deadSockets.Add(connId);
            }
        }

        foreach (var deadId in deadSockets)
        {
            CleanupSocket(deadId);
        }
    }

    public async Task BroadcastToKitchenAsync(string eventName, object? data)
    {
        await BroadcastToRoomAsync("kitchen", eventName, data);
    }

    public async Task BroadcastToOrderAsync(string orderToken, string eventName, object? data)
    {
        await BroadcastToRoomAsync($"order:{orderToken}", eventName, data);
    }

    public async Task BroadcastToTableAsync(string mesa, string eventName, object? data)
    {
        await BroadcastToRoomAsync($"table:{mesa}", eventName, data);
    }

    public async Task BroadcastAllAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var deadSockets = new List<string>();

        foreach (var (connId, socket) in _sockets)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    deadSockets.Add(connId);
                }
            }
            else
            {
                deadSockets.Add(connId);
            }
        }

        foreach (var deadId in deadSockets)
        {
            CleanupSocket(deadId);
        }
    }

    private static async Task SendJsonAsync(WebSocket socket, object payload)
    {
        if (socket.State == WebSocketState.Open)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
