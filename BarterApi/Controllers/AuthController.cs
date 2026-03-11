using System.Security.Claims;
using BarterApi.Data;
using BarterApi.DTOs;
using BarterApi.Models;
using BarterApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly EmailService _email;
    private readonly GoogleAuthService _google;

    public AuthController(AppDbContext db, JwtService jwt, EmailService email, GoogleAuthService google)
    {
        _db = db;
        _jwt = jwt;
        _email = email;
        _google = google;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { error = "Email already in use" });

        var user = new User
        {
            Name = req.Name,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            EmailVerified = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Send verification OTP
        await SendOtpToUser(user);

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, MapUser(user)));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null || user.PasswordHash == null)
            return Unauthorized(new { error = "Invalid email or password" });

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password" });

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, MapUser(user)));
    }

    // POST /api/auth/google
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
    {
        var googleUser = await _google.VerifyGoogleTokenAsync(req.IdToken);
        if (googleUser == null)
            return Unauthorized(new { error = "Invalid Google token" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == googleUser.Email);
        if (user == null)
        {
            // New user — create account
            user = new User
            {
                Name = googleUser.Name,
                Email = googleUser.Email,
                PhotoUrl = googleUser.PhotoUrl,
                EmailVerified = googleUser.EmailVerified,
                PasswordHash = null // Google users have no password
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else
        {
            // Update photo if needed
            if (user.PhotoUrl == null && googleUser.PhotoUrl != null)
            {
                user.PhotoUrl = googleUser.PhotoUrl;
                await _db.SaveChangesAsync();
            }
        }

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, MapUser(user)));
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return Ok(); // Don't reveal whether email exists

        // In production: generate reset token, save to DB, send link
        // For now, generate a new temp password and email it
        var tempPassword = Guid.NewGuid().ToString("N")[..8];
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetEmailAsync(req.Email, $"Temporary password: {tempPassword}");
        return Ok(new { message = "Reset email sent" });
    }

    // POST /api/auth/send-otp
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req)
    {
        var user = await _db.Users.FindAsync(req.UserId);
        if (user == null) return NotFound(new { error = "User not found" });

        await SendOtpToUser(user);
        return Ok(new { message = "OTP sent" });
    }

    // POST /api/auth/verify-otp
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        var user = await _db.Users.FindAsync(req.UserId);
        if (user == null) return NotFound(new { error = "User not found" });

        if (user.CurrentOtp == null || user.OtpExpiresAt == null)
            return BadRequest(new { error = "No OTP was requested" });

        if (DateTime.UtcNow > user.OtpExpiresAt)
            return BadRequest(new { error = "OTP has expired" });

        if (user.CurrentOtp != req.Code)
            return BadRequest(new { error = "Invalid OTP" });

        // Mark email as verified & clear OTP
        user.EmailVerified = true;
        user.CurrentOtp = null;
        user.OtpExpiresAt = null;
        await _db.SaveChangesAsync();

        return Ok(new { verified = true });
    }

    // PUT /api/auth/mfa
    [HttpPut("mfa")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.MfaEnabled = req.Enabled;
        await _db.SaveChangesAsync();
        return Ok(new { mfaEnabled = user.MfaEnabled });
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(MapUser(user));
    }

    // ===== Helpers =====
    private async Task SendOtpToUser(User user)
    {
        var otp = new Random().Next(100000, 999999).ToString();
        user.CurrentOtp = otp;
        user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        await _db.SaveChangesAsync();
        await _email.SendOtpEmailAsync(user.Email, otp);
    }

    private static UserDto MapUser(User u) => new(
        u.Id, u.Name, u.Email, u.PhotoUrl, u.Phone, u.Location,
        u.EmailVerified, u.MfaEnabled, u.MfaMethod,
        u.RatingSum, u.ReviewCount, u.AverageRating, u.CreatedAt
    );
}
