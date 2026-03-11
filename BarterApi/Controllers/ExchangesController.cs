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
[Route("api/exchanges")]
[Authorize]
public class ExchangesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExchangesController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/exchanges
    [HttpGet]
    public async Task<IActionResult> GetUserExchanges()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var exchanges = await _db.Exchanges
            .Where(e => e.ProposedBy == userId || e.ProposedTo == userId)
            .OrderByDescending(e => e.ProposedAt)
            .ToListAsync();
        return Ok(exchanges.Select(MapExchange));
    }

    // GET /api/exchanges/{id}
    [HttpGet("{id}", Name = nameof(GetExchange))]
    public async Task<IActionResult> GetExchange(string id)
    {
        var exchange = await _db.Exchanges.FindAsync(id);
        if (exchange == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        if (exchange.ProposedBy != userId && exchange.ProposedTo != userId)
            return Forbid();

        return Ok(MapExchange(exchange));
    }

    // GET /api/exchanges/pending  — incoming pending only
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingExchanges()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var exchanges = await _db.Exchanges
            .Where(e => e.ProposedTo == userId && e.Status == ExchangeStatus.Pending)
            .OrderByDescending(e => e.ProposedAt)
            .ToListAsync();
        return Ok(exchanges.Select(MapExchange));
    }

    // POST /api/exchanges
    [HttpPost]
    public async Task<IActionResult> CreateExchange([FromBody] CreateExchangeRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

        // Check items are not already exchanged
        var offeredIds = req.ItemsOffered.Select(i => i.ItemId).ToList();
        var requestedIds = req.ItemsRequested.Select(i => i.ItemId).ToList();
        var allIds = offeredIds.Concat(requestedIds).ToList();

        var conflictingItems = await _db.Items
            .Where(i => allIds.Contains(i.Id) && i.IsExchanged)
            .Select(i => i.Title)
            .ToListAsync();

        if (conflictingItems.Any())
            return BadRequest(new { error = $"Item(s) already exchanged: {string.Join(", ", conflictingItems)}" });

        // Check for duplicate pending request
        var existing = await _db.Exchanges
            .Where(e => e.ProposedBy == userId && e.ProposedTo == req.ProposedTo && e.Status == ExchangeStatus.Pending)
            .ToListAsync();

        foreach (var ex in existing)
        {
            var exOffered = JsonSerializer.Deserialize<List<ExchangeItemDto>>(ex.ItemsOfferedJson)?
                .Select(i => i.ItemId).ToHashSet() ?? new();
            var exRequested = JsonSerializer.Deserialize<List<ExchangeItemDto>>(ex.ItemsRequestedJson)?
                .Select(i => i.ItemId).ToHashSet() ?? new();

            if (exOffered.SetEquals(offeredIds) && exRequested.SetEquals(requestedIds))
                return Conflict(new { error = "You have already sent this exact exchange request." });
        }

        // We call the chat creation logic directly via DbContext
        var allChats = await _db.Chats.ToListAsync();
        var existingChat = allChats.FirstOrDefault(c =>
        {
            var parts = JsonSerializer.Deserialize<List<string>>(c.ParticipantsJson) ?? new();
            return parts.Contains(userId) && parts.Contains(req.ProposedTo);
        });

        string chatId;
        if (existingChat != null)
        {
            chatId = existingChat.Id;
        }
        else
        {
            var chat = new Chat
            {
                ParticipantsJson = JsonSerializer.Serialize(new[] { userId, req.ProposedTo }),
                ItemId = req.ItemsRequested.First().ItemId,
                ItemTitle = req.ItemsRequested.First().Title,
                LastMessageTime = DateTime.UtcNow,
                UnreadCountsJson = JsonSerializer.Serialize(new Dictionary<string, int>
                {
                    { userId, 0 },
                    { req.ProposedTo, 0 }
                })
            };
            _db.Chats.Add(chat);
            await _db.SaveChangesAsync();
            chatId = chat.Id;
        }

        var exchange = new Exchange
        {
            ProposedBy = userId,
            ProposedTo = req.ProposedTo,
            ItemsOfferedJson = JsonSerializer.Serialize(req.ItemsOffered),
            ItemsRequestedJson = JsonSerializer.Serialize(req.ItemsRequested),
            Notes = req.Notes,
            ChatId = chatId,
            Status = ExchangeStatus.Pending
        };

        _db.Exchanges.Add(exchange);

        // Create notification for receiver
        _db.Notifications.Add(new Notification
        {
            UserId = req.ProposedTo,
            Title = "New Exchange Request",
            Body = "You have a new exchange request!",
            Type = NotificationType.ExchangeRequest,
            RelatedId = exchange.Id
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetExchange), new { id = exchange.Id }, MapExchange(exchange));
    }

    // PUT /api/exchanges/{id}/accept
    [HttpPut("{id}/accept")]
    public async Task<IActionResult> AcceptExchange(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var exchange = await _db.Exchanges.FindAsync(id);
        if (exchange == null) return NotFound();
        if (exchange.ProposedTo != userId) return Forbid();
        if (exchange.Status != ExchangeStatus.Pending)
            return BadRequest(new { error = "Exchange is not pending" });

        exchange.Status = ExchangeStatus.Accepted;
        exchange.AcceptedAt = DateTime.UtcNow;

        // Mark all items as unavailable/exchanged
        var offeredIds = JsonSerializer.Deserialize<List<ExchangeItemDto>>(exchange.ItemsOfferedJson)?
            .Select(i => i.ItemId).ToList() ?? new();
        var requestedIds = JsonSerializer.Deserialize<List<ExchangeItemDto>>(exchange.ItemsRequestedJson)?
            .Select(i => i.ItemId).ToList() ?? new();
        var allIds = offeredIds.Concat(requestedIds).ToList();

        await _db.Items
            .Where(i => allIds.Contains(i.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.IsAvailable, false)
                .SetProperty(i => i.IsExchanged, true));

        // Auto-cancel other pending exchanges involving these items
        var others = await _db.Exchanges
            .Where(e => e.Id != id && e.Status == ExchangeStatus.Pending)
            .ToListAsync();

        foreach (var other in others)
        {
            var otherIds = new List<string>();
            var oi = JsonSerializer.Deserialize<List<ExchangeItemDto>>(other.ItemsOfferedJson)?.Select(i => i.ItemId) ?? Enumerable.Empty<string>();
            var ri = JsonSerializer.Deserialize<List<ExchangeItemDto>>(other.ItemsRequestedJson)?.Select(i => i.ItemId) ?? Enumerable.Empty<string>();
            otherIds.AddRange(oi);
            otherIds.AddRange(ri);

            if (otherIds.Any(i => allIds.Contains(i)))
            {
                other.Status = ExchangeStatus.Cancelled;
                _db.Notifications.Add(new Notification
                {
                    UserId = other.ProposedBy,
                    Title = "Exchange Cancelled",
                    Body = "An exchange you proposed was cancelled because the items are no longer available.",
                    Type = NotificationType.ExchangeCancelled,
                    RelatedId = other.Id
                });
            }
        }

        // Notify proposer
        _db.Notifications.Add(new Notification
        {
            UserId = exchange.ProposedBy,
            Title = "Exchange Accepted",
            Body = "Your exchange request has been accepted!",
            Type = NotificationType.ExchangeAccepted,
            RelatedId = exchange.Id
        });

        await _db.SaveChangesAsync();
        return Ok(MapExchange(exchange));
    }

    // PUT /api/exchanges/{id}/cancel
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelExchange(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var exchange = await _db.Exchanges.FindAsync(id);
        if (exchange == null) return NotFound();
        if (exchange.ProposedBy != userId && exchange.ProposedTo != userId) return Forbid();

        // If accepted, make items available again
        if (exchange.Status == ExchangeStatus.Accepted)
        {
            var offeredIds = JsonSerializer.Deserialize<List<ExchangeItemDto>>(exchange.ItemsOfferedJson)?
                .Select(i => i.ItemId).ToList() ?? new();
            var requestedIds = JsonSerializer.Deserialize<List<ExchangeItemDto>>(exchange.ItemsRequestedJson)?
                .Select(i => i.ItemId).ToList() ?? new();
            var allIds = offeredIds.Concat(requestedIds).ToList();

            await _db.Items
                .Where(i => allIds.Contains(i.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.IsAvailable, true)
                    .SetProperty(i => i.IsExchanged, false));
        }

        exchange.Status = ExchangeStatus.Cancelled;

        var otherUserId = exchange.ProposedBy == userId ? exchange.ProposedTo : exchange.ProposedBy;
        _db.Notifications.Add(new Notification
        {
            UserId = otherUserId,
            Title = "Exchange Cancelled",
            Body = "An exchange request was cancelled.",
            Type = NotificationType.ExchangeCancelled,
            RelatedId = exchange.Id
        });

        await _db.SaveChangesAsync();
        return Ok(MapExchange(exchange));
    }

    // PUT /api/exchanges/{id}/confirm
    [HttpPut("{id}/confirm")]
    public async Task<IActionResult> ConfirmCompletion(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var exchange = await _db.Exchanges.FindAsync(id);
        if (exchange == null) return NotFound();

        var confirmed = JsonSerializer.Deserialize<List<string>>(exchange.ConfirmedByJson) ?? new();
        if (confirmed.Contains(userId))
            return BadRequest(new { error = "Already confirmed" });

        confirmed.Add(userId);
        exchange.ConfirmedByJson = JsonSerializer.Serialize(confirmed);

        var bothConfirmed = confirmed.Contains(exchange.ProposedBy) && confirmed.Contains(exchange.ProposedTo);
        if (bothConfirmed)
        {
            exchange.Status = ExchangeStatus.Completed;
            exchange.CompletedAt = DateTime.UtcNow;

            foreach (var uid in new[] { exchange.ProposedBy, exchange.ProposedTo })
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Title = "Exchange Completed",
                    Body = "The exchange has been successfully completed!",
                    Type = NotificationType.ExchangeCompleted,
                    RelatedId = id
                });
            }
        }
        else
        {
            var otherUserId = exchange.ProposedBy == userId ? exchange.ProposedTo : exchange.ProposedBy;
            _db.Notifications.Add(new Notification
            {
                UserId = otherUserId,
                Title = "Exchange Confirmation",
                Body = "The other party has confirmed the exchange completion.",
                Type = NotificationType.ExchangeCompleted,
                RelatedId = id
            });
        }

        await _db.SaveChangesAsync();
        return Ok(MapExchange(exchange));
    }

    // PUT /api/exchanges/{id}/meeting
    [HttpPut("{id}/meeting")]
    public async Task<IActionResult> UpdateMeeting(string id, [FromBody] UpdateMeetingRequest req)
    {
        var exchange = await _db.Exchanges.FindAsync(id);
        if (exchange == null) return NotFound();

        if (req.Location != null) exchange.MeetingLocation = req.Location;
        if (req.Date != null) exchange.MeetingDate = req.Date;

        await _db.SaveChangesAsync();
        return Ok(MapExchange(exchange));
    }

    // POST /api/exchanges/{id}/rate
    [HttpPost("{id}/rate")]
    public async Task<IActionResult> RateExchange(string id, [FromBody] RateExchangeRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var exchange = await _db.Exchanges.FindAsync(id);
        if (exchange == null) return NotFound();
        if (exchange.Status != ExchangeStatus.Completed)
            return BadRequest(new { error = "Can only rate completed exchanges" });

        var revieweeId = exchange.ProposedBy == userId ? exchange.ProposedTo : exchange.ProposedBy;

        if (exchange.ProposedBy == userId)
        {
            exchange.RatingByProposer = req.Rating;
            exchange.ReviewByProposer = req.Review;
        }
        else
        {
            exchange.RatingByAccepter = req.Rating;
            exchange.ReviewByAccepter = req.Review;
        }

        // Save review + update user stats
        var review = new Review
        {
            ReviewerId = userId,
            RevieweeId = revieweeId,
            ExchangeId = id,
            Rating = req.Rating,
            Comment = req.Review ?? ""
        };
        _db.Reviews.Add(review);

        var reviewee = await _db.Users.FindAsync(revieweeId);
        if (reviewee != null)
        {
            reviewee.RatingSum += req.Rating;
            reviewee.ReviewCount++;
        }

        await _db.SaveChangesAsync();
        return Ok(MapExchange(exchange));
    }

    // ===== Helpers =====
    private static ExchangeDto MapExchange(Exchange e) => new(
        e.Id, (int)e.Status, e.ProposedBy, e.ProposedTo,
        JsonSerializer.Deserialize<List<ExchangeItemDto>>(e.ItemsOfferedJson) ?? new(),
        JsonSerializer.Deserialize<List<ExchangeItemDto>>(e.ItemsRequestedJson) ?? new(),
        e.ProposedAt, e.AcceptedAt, e.CompletedAt,
        JsonSerializer.Deserialize<List<string>>(e.ConfirmedByJson) ?? new(),
        e.MeetingLocation, e.MeetingDate, e.Notes, e.ChatId,
        e.RatingByProposer, e.RatingByAccepter,
        e.ReviewByProposer, e.ReviewByAccepter
    );
}
