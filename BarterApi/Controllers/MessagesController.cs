using System.Security.Claims;
using System.Text.Json;
using BarterApi.Data;
using BarterApi.DTOs;
using BarterApi.Hubs;
using BarterApi.Models;
using BarterApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/chats/{chatId}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ImageService _imageService;

    public MessagesController(AppDbContext db, IHubContext<ChatHub> hub, ImageService imageService)
    {
        _db = db;
        _hub = hub;
        _imageService = imageService;
    }

    // GET /api/chats/{chatId}/messages
    [HttpGet]
    public async Task<IActionResult> GetMessages(string chatId)
    {
        var messages = await _db.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.Timestamp)
            .Select(m => MapMessage(m))
            .ToListAsync();
        return Ok(messages);
    }

    // POST /api/chats/{chatId}/messages  — send text message
    [HttpPost]
    public async Task<IActionResult> SendMessage(string chatId, [FromBody] SendMessageRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

        var chat = await _db.Chats.FindAsync(chatId);
        if (chat == null) return NotFound(new { error = "Chat not found" });

        var blockedBy = JsonSerializer.Deserialize<List<string>>(chat.BlockedByJson) ?? new();
        if (blockedBy.Count > 0)
            return BadRequest(new { error = "Cannot send message. This chat is blocked." });

        var message = new Message
        {
            ChatId = chatId,
            SenderId = userId,
            Content = req.Content,
            MessageType = MessageType.Text
        };

        _db.Messages.Add(message);

        // Update chat last message
        chat.LastMessage = req.Content;
        chat.LastMessageTime = DateTime.UtcNow;
        chat.LastSenderId = userId;

        // Increment unread for other participants
        var parts = JsonSerializer.Deserialize<List<string>>(chat.ParticipantsJson) ?? new();
        var unread = JsonSerializer.Deserialize<Dictionary<string, int>>(chat.UnreadCountsJson) ?? new();
        foreach (var p in parts.Where(p => p != userId))
            unread[p] = (unread.TryGetValue(p, out var c) ? c : 0) + 1;
        chat.UnreadCountsJson = JsonSerializer.Serialize(unread);

        await _db.SaveChangesAsync();

        var dto = MapMessage(message);

        // Push to SignalR group
        await _hub.Clients.Group(chatId).SendAsync("ReceiveMessage", dto);

        return Ok(dto);
    }

    // POST /api/chats/{chatId}/messages/photo  — send photo message
    [HttpPost("photo")]
    public async Task<IActionResult> SendPhotoMessage(string chatId, IFormFile photo)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

        var chat = await _db.Chats.FindAsync(chatId);
        if (chat == null) return NotFound(new { error = "Chat not found" });

        var blockedBy = JsonSerializer.Deserialize<List<string>>(chat.BlockedByJson) ?? new();
        if (blockedBy.Count > 0)
            return BadRequest(new { error = "Cannot send photo. This chat is blocked." });

        var photoUrl = await _imageService.UploadImageAsync(photo);

        var message = new Message
        {
            ChatId = chatId,
            SenderId = userId,
            Content = "Photo",
            MessageType = MessageType.Photo,
            PhotoUrl = photoUrl
        };

        _db.Messages.Add(message);

        chat.LastMessage = "📷 Photo";
        chat.LastMessageTime = DateTime.UtcNow;
        chat.LastSenderId = userId;

        var parts = JsonSerializer.Deserialize<List<string>>(chat.ParticipantsJson) ?? new();
        var unread = JsonSerializer.Deserialize<Dictionary<string, int>>(chat.UnreadCountsJson) ?? new();
        foreach (var p in parts.Where(p => p != userId))
            unread[p] = (unread.TryGetValue(p, out var c) ? c : 0) + 1;
        chat.UnreadCountsJson = JsonSerializer.Serialize(unread);

        await _db.SaveChangesAsync();

        var dto = MapMessage(message);
        await _hub.Clients.Group(chatId).SendAsync("ReceiveMessage", dto);

        return Ok(dto);
    }

    // ===== Helpers =====
    private static MessageDto MapMessage(Message m) => new(
        m.Id, m.ChatId, m.SenderId, m.Content,
        m.MessageType == MessageType.Photo ? "photo" : "text",
        m.PhotoUrl, m.IsRead, m.Timestamp
    );
}
