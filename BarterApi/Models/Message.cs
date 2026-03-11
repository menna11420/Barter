using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public enum MessageType
{
    Text,
    Photo
}

public class Message
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string ChatId { get; set; } = string.Empty;
    
    [Required]
    public string SenderId { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public MessageType MessageType { get; set; } = MessageType.Text;
    public string? PhotoUrl { get; set; }
    
    public bool IsRead { get; set; } = false;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Navigation
    [ForeignKey("ChatId")]
    public Chat? Chat { get; set; }
    
    [ForeignKey("SenderId")]
    public User? Sender { get; set; }
}
