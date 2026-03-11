using System.Security.Claims;
using BarterApi.Data;
using BarterApi.DTOs;
using BarterApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/notifications
    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var notifs = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => MapNotif(n))
            .ToListAsync();
        return Ok(notifs);
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var count = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(new { count });
    }

    // PUT /api/notifications/{id}/read
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var notif = await _db.Notifications.FindAsync(id);
        if (notif == null) return NotFound();
        if (notif.UserId != userId) return Forbid();

        notif.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok();
    }

    private static NotificationDto MapNotif(Notification n) => new(
        n.Id, n.UserId, n.Title, n.Body, (int)n.Type,
        n.RelatedId, n.IsRead, n.CreatedAt
    );
}
