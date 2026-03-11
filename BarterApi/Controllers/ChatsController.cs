using System.Security.Claims;
using System.Text.Json;
using BarterApi.Data;
using BarterApi.DTOs;
using BarterApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ChatsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/chats  — get current user's chats
    [HttpGet]
    public async Task<IActionResult> GetUserChats()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var chats = await _db.Chats.ToListAsync();

        var userChats = chats
            .Where(c =>
            {
                var parts = JsonSerializer.Deserialize<List<string>>(c.ParticipantsJson) ?? new();
                return parts.Contains(userId);
            })
            .OrderByDescending(c => c.LastMessageTime)
            .Select(c => MapChat(c, userId))
            .ToList();

        return Ok(userChats);
    }

    // POST /api/chats  — create or get existing chat
    [HttpPost]
    public async Task<IActionResult> CreateOrGetChat([FromBody] CreateChatRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

        // Look for existing chat between these two users
        var allChats = await _db.Chats.ToListAsync();
        var existing = allChats.FirstOrDefault(c =>
        {
            var parts = JsonSerializer.Deserialize<List<string>>(c.ParticipantsJson) ?? new();
            return parts.Contains(userId) && parts.Contains(req.OtherUserId);
        });

        if (existing != null)
        {
            existing.ItemId = req.ItemId;
            existing.ItemTitle = req.ItemTitle;
            existing.LastMessageTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(MapChat(existing, userId));
        }

        var chat = new Chat
        {
            ParticipantsJson = JsonSerializer.Serialize(new[] { userId, req.OtherUserId }),
            ItemId = req.ItemId,
            ItemTitle = req.ItemTitle,
            LastMessageTime = DateTime.UtcNow,
            UnreadCountsJson = JsonSerializer.Serialize(new Dictionary<string, int>
            {
                { userId, 0 },
                { req.OtherUserId, 0 }
            })
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();
        return Ok(MapChat(chat, userId));
    }

    // PUT /api/chats/{chatId}/read
    [HttpPut("{chatId}/read")]
    public async Task<IActionResult> MarkAsRead(string chatId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var chat = await _db.Chats.FindAsync(chatId);
        if (chat == null) return NotFound();

        var unreadCounts = JsonSerializer.Deserialize<Dictionary<string, int>>(chat.UnreadCountsJson)
                           ?? new Dictionary<string, int>();
        unreadCounts[userId] = 0;
        chat.UnreadCountsJson = JsonSerializer.Serialize(unreadCounts);
        await _db.SaveChangesAsync();

        return Ok();
    }

    // POST /api/chats/{chatId}/block
    [HttpPost("{chatId}/block")]
    public async Task<IActionResult> BlockUser(string chatId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var chat = await _db.Chats.FindAsync(chatId);
        if (chat == null) return NotFound();

        var blockedBy = JsonSerializer.Deserialize<List<string>>(chat.BlockedByJson) ?? new();
        if (!blockedBy.Contains(userId)) blockedBy.Add(userId);
        chat.BlockedByJson = JsonSerializer.Serialize(blockedBy);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/chats/{chatId}/unblock
    [HttpPost("{chatId}/unblock")]
    public async Task<IActionResult> UnblockUser(string chatId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var chat = await _db.Chats.FindAsync(chatId);
        if (chat == null) return NotFound();

        var blockedBy = JsonSerializer.Deserialize<List<string>>(chat.BlockedByJson) ?? new();
        blockedBy.Remove(userId);
        chat.BlockedByJson = JsonSerializer.Serialize(blockedBy);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // GET /api/chats/unread-count  — total unread across all chats
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetTotalUnreadCount()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var chats = await _db.Chats.ToListAsync();
        int total = 0;

        foreach (var c in chats)
        {
            var parts = JsonSerializer.Deserialize<List<string>>(c.ParticipantsJson) ?? new();
            if (!parts.Contains(userId)) continue;

            var unread = JsonSerializer.Deserialize<Dictionary<string, int>>(c.UnreadCountsJson) ?? new();
            if (unread.TryGetValue(userId, out var count)) total += count;
        }

        return Ok(new { total });
    }

    // ===== Helpers =====
    private static ChatDto MapChat(Chat c, string currentUserId)
    {
        var parts = JsonSerializer.Deserialize<List<string>>(c.ParticipantsJson) ?? new();
        var blocked = JsonSerializer.Deserialize<List<string>>(c.BlockedByJson) ?? new();
        var unread = JsonSerializer.Deserialize<Dictionary<string, int>>(c.UnreadCountsJson) ?? new();
        unread.TryGetValue(currentUserId, out var unreadCount);

        return new ChatDto(
            c.Id, parts, c.ItemId, c.ItemTitle,
            c.LastMessage, c.LastMessageTime, c.LastSenderId,
            unreadCount, blocked
        );
    }
}
