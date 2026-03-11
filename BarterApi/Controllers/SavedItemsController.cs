using System.Security.Claims;
using BarterApi.Data;
using BarterApi.DTOs;
using BarterApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/saved")]
[Authorize]
public class SavedItemsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SavedItemsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/saved
    [HttpGet]
    public async Task<IActionResult> GetSavedItems()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var itemIds = await _db.SavedItems
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => s.ItemId)
            .ToListAsync();
        return Ok(itemIds);
    }

    // GET /api/saved/{itemId}/check
    [HttpGet("{itemId}/check")]
    public async Task<IActionResult> IsItemSaved(string itemId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var isSaved = await _db.SavedItems.AnyAsync(s => s.UserId == userId && s.ItemId == itemId);
        return Ok(new { isSaved });
    }

    // POST /api/saved/{itemId}
    [HttpPost("{itemId}")]
    public async Task<IActionResult> SaveItem(string itemId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

        if (await _db.SavedItems.AnyAsync(s => s.UserId == userId && s.ItemId == itemId))
            return Ok(new { saved = true }); // Already saved

        _db.SavedItems.Add(new SavedItem { UserId = userId, ItemId = itemId });
        await _db.SaveChangesAsync();
        return Ok(new { saved = true });
    }

    // DELETE /api/saved/{itemId}
    [HttpDelete("{itemId}")]
    public async Task<IActionResult> UnsaveItem(string itemId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var saved = await _db.SavedItems.FirstOrDefaultAsync(s => s.UserId == userId && s.ItemId == itemId);
        if (saved == null) return Ok(new { saved = false });

        _db.SavedItems.Remove(saved);
        await _db.SaveChangesAsync();
        return Ok(new { saved = false });
    }

    // POST /api/saved/{itemId}/toggle
    [HttpPost("{itemId}/toggle")]
    public async Task<IActionResult> ToggleSavedItem(string itemId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        var existing = await _db.SavedItems.FirstOrDefaultAsync(s => s.UserId == userId && s.ItemId == itemId);

        if (existing != null)
        {
            _db.SavedItems.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { saved = false });
        }

        _db.SavedItems.Add(new SavedItem { UserId = userId, ItemId = itemId });
        await _db.SaveChangesAsync();
        return Ok(new { saved = true });
    }
}
