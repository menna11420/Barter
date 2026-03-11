using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public class Chat
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    // Stored as JSON array of user IDs
    public string ParticipantsJson { get; set; } = "[]";
    
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
    public string LastSenderId { get; set; } = string.Empty;
    
    // Stored as JSON: { "userId": unreadCount }
    public string UnreadCountsJson { get; set; } = "{}";
    
    // Stored as JSON array of user IDs who blocked
    public string BlockedByJson { get; set; } = "[]";
    
    // Navigation
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Exchange> Exchanges { get; set; } = new List<Exchange>();
}
