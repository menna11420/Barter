using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public enum NotificationType
{
    ExchangeRequest = 0,
    ExchangeAccepted = 1,
    ExchangeCancelled = 2,
    ExchangeCompleted = 3,
    NewMessage = 4,
    Other = 5
}

public class Notification
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Body { get; set; } = string.Empty;
    
    public NotificationType Type { get; set; } = NotificationType.Other;
    
    public string? RelatedId { get; set; }
    
    public bool IsRead { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    [ForeignKey("UserId")]
    public User? User { get; set; }
}
