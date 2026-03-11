using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public class User
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    public string? PasswordHash { get; set; } // null for Google-OAuth users
    
    public string? PhotoUrl { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    
    public bool EmailVerified { get; set; } = false;
    public bool MfaEnabled { get; set; } = false;
    public string MfaMethod { get; set; } = "email";
    
    public double RatingSum { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;
    
    [NotMapped]
    public double AverageRating => ReviewCount == 0 ? 0 : RatingSum / ReviewCount;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // OTP
    public string? CurrentOtp { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    
    // Navigation
    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
    public ICollection<Review> ReviewsGiven { get; set; } = new List<Review>();
    public ICollection<SavedItem> SavedItems { get; set; } = new List<SavedItem>();
}
