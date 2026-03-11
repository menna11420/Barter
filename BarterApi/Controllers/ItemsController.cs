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
[Route("api/items")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ItemsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/items   (public)
    [HttpGet]
    public async Task<IActionResult> GetItems()
    {
        var items = await _db.Items
            .Where(i => i.IsAvailable)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => MapItem(i))
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/items/{id}  (public)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(string id)
    {
        var item = await _db.Items.FindAsync(id);
        if (item == null) return NotFound(new { error = "Item not found" });
        return Ok(MapItem(item));
    }

    // GET /api/items/user/{userId}  (public)
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserItems(string userId)
    {
        var items = await _db.Items
            .Where(i => i.OwnerId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => MapItem(i))
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/items/search?q=xxx  (public)
    [HttpGet("search")]
    public async Task<IActionResult> SearchItems([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<ItemDto>());

        var lower = q.ToLower();
        var items = await _db.Items
            .Where(i => i.IsAvailable &&
                        (i.Title.ToLower().Contains(lower) || i.Description.ToLower().Contains(lower)))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => MapItem(i))
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/items/nearby?lat=X&lng=Y&radius=Z  (public)
    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearbyItems(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radius = 10,
        [FromQuery] int? category = null)
    {
        var query = _db.Items.Where(i => i.IsAvailable && i.Latitude != null && i.Longitude != null);
        if (category != null) query = query.Where(i => (int)i.Category == category);

        var allItems = await query.ToListAsync();

        // Filter by radius (Haversine approximation in memory)
        var nearby = allItems
            .Where(i => GetDistanceKm(lat, lng, i.Latitude!.Value, i.Longitude!.Value) <= radius)
            .OrderBy(i => GetDistanceKm(lat, lng, i.Latitude!.Value, i.Longitude!.Value))
            .Select(i => MapItem(i))
            .ToList();

        return Ok(nearby);
    }

    // GET /api/items/map  (public)
    [HttpGet("map")]
    public async Task<IActionResult> GetItemsForMap()
    {
        var items = await _db.Items
            .Where(i => i.IsAvailable && i.Latitude != null && i.Longitude != null)
            .Select(i => MapItem(i))
            .ToListAsync();
        return Ok(items);
    }

    // POST /api/items  (auth)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var item = new Item
        {
            OwnerId = userId,
            OwnerName = user.Name,
            Title = req.Title,
            Description = req.Description,
            ImageUrlsJson = JsonSerializer.Serialize(req.ImageUrls),
            Category = (ItemCategory)req.Category,
            Condition = (ItemCondition)req.Condition,
            PreferredExchange = req.PreferredExchange,
            Location = req.Location,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            DetailedAddress = req.DetailedAddress,
            ItemType = (ItemType)req.ItemType,
            IsRemote = req.IsRemote
        };

        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, MapItem(item));
    }

    // PUT /api/items/{id}  (auth, owner only)
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateItem(string id, [FromBody] UpdateItemRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var item = await _db.Items.FindAsync(id);
        if (item == null) return NotFound();
        if (item.OwnerId != userId) return Forbid();

        if (req.Title != null) item.Title = req.Title;
        if (req.Description != null) item.Description = req.Description;
        if (req.ImageUrls != null) item.ImageUrlsJson = JsonSerializer.Serialize(req.ImageUrls);
        if (req.Category != null) item.Category = (ItemCategory)req.Category;
        if (req.Condition != null) item.Condition = (ItemCondition)req.Condition;
        if (req.PreferredExchange != null) item.PreferredExchange = req.PreferredExchange;
        if (req.Location != null) item.Location = req.Location;
        if (req.Latitude != null) item.Latitude = req.Latitude;
        if (req.Longitude != null) item.Longitude = req.Longitude;
        if (req.DetailedAddress != null) item.DetailedAddress = req.DetailedAddress;
        if (req.IsAvailable != null) item.IsAvailable = req.IsAvailable.Value;
        if (req.ItemType != null) item.ItemType = (ItemType)req.ItemType;
        if (req.IsRemote != null) item.IsRemote = req.IsRemote.Value;

        await _db.SaveChangesAsync();
        return Ok(MapItem(item));
    }

    // DELETE /api/items/{id}  (auth, owner only)
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteItem(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var item = await _db.Items.FindAsync(id);
        if (item == null) return NotFound();
        if (item.OwnerId != userId) return Forbid();

        // Auto-cancel pending exchanges via service logic
        var pendingExchanges = await _db.Exchanges
            .Where(e => e.Status == ExchangeStatus.Pending &&
                        (e.ItemsOfferedJson.Contains(id) || e.ItemsRequestedJson.Contains(id)))
            .ToListAsync();

        foreach (var ex in pendingExchanges)
            ex.Status = ExchangeStatus.Cancelled;

        _db.Items.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ===== Helpers =====
    internal static ItemDto MapItem(Item i) => new(
        i.Id, i.OwnerId, i.OwnerName, i.Title, i.Description,
        JsonSerializer.Deserialize<List<string>>(i.ImageUrlsJson) ?? new(),
        (int)i.Category, (int)i.Condition, i.PreferredExchange,
        i.Location, i.Latitude, i.Longitude, i.DetailedAddress,
        i.CreatedAt, i.IsAvailable, i.IsExchanged, (int)i.ItemType, i.IsRemote
    );

    private static double GetDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
