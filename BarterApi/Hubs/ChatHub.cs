using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BarterApi.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} connected to ChatHub", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} disconnected from ChatHub", userId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Join a chat room to receive real-time messages</summary>
    public async Task JoinChat(string chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        _logger.LogInformation("Connection {ConnectionId} joined chat {ChatId}", Context.ConnectionId, chatId);
    }

    /// <summary>Leave a chat room</summary>
    public async Task LeaveChat(string chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
    }

    /// <summary>
    /// Real-time message push — called by MessagesController after saving to DB.
    /// This is also exposed as a hub method so the Flutter app can call it directly.
    /// </summary>
    public async Task NotifyMessage(string chatId, object messagePayload)
    {
        await Clients.Group(chatId).SendAsync("ReceiveMessage", messagePayload);
    }
}
