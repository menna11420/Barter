using System.Security.Claims;
using BarterApi.Data;
using BarterApi.DTOs;
using BarterApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/users/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "User not found" });
        return Ok(MapUser(user));
    }

    // GET /api/users/me
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();
        return Ok(MapUser(user));
    }

    // PUT /api/users/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest req)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (id != currentUserId) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (req.Name != null) user.Name = req.Name;
        if (req.PhotoUrl != null) user.PhotoUrl = req.PhotoUrl;
        if (req.Phone != null) user.Phone = req.Phone;
        if (req.Location != null) user.Location = req.Location;

        await _db.SaveChangesAsync();
        return Ok(MapUser(user));
    }

    // GET /api/users/{id}/reviews
    [HttpGet("{id}/reviews")]
    public async Task<IActionResult> GetUserReviews(string id)
    {
        var reviews = await _db.Reviews
            .Where(r => r.RevieweeId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapReview(r))
            .ToListAsync();
        return Ok(reviews);
    }

    // ===== Helpers =====
    private static UserDto MapUser(User u) => new(
        u.Id, u.Name, u.Email, u.PhotoUrl, u.Phone, u.Location,
        u.EmailVerified, u.MfaEnabled, u.MfaMethod,
        u.RatingSum, u.ReviewCount, u.AverageRating, u.CreatedAt
    );

    private static ReviewDto MapReview(Review r) => new(
        r.Id, r.ReviewerId, r.RevieweeId, r.ExchangeId,
        r.Rating, r.Comment, r.CreatedAt
    );
}
