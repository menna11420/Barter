using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public enum ExchangeStatus
{
    Pending = 0,
    Accepted = 1,
    Completed = 2,
    Cancelled = 3
}

public class Exchange
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public ExchangeStatus Status { get; set; } = ExchangeStatus.Pending;
    
    [Required]
    public string ProposedBy { get; set; } = string.Empty;
    
    [Required]
    public string ProposedTo { get; set; } = string.Empty;
    
    // Stored as JSON
    public string ItemsOfferedJson { get; set; } = "[]";
    public string ItemsRequestedJson { get; set; } = "[]";
    
    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Stored as JSON array of user IDs
    public string ConfirmedByJson { get; set; } = "[]";
    
    public string? MeetingLocation { get; set; }
    public DateTime? MeetingDate { get; set; }
    public string? Notes { get; set; }
    
    [Required]
    public string ChatId { get; set; } = string.Empty;
    
    public double? RatingByProposer { get; set; }
    public double? RatingByAccepter { get; set; }
    public string? ReviewByProposer { get; set; }
    public string? ReviewByAccepter { get; set; }
    
    // Navigation
    [ForeignKey("ProposedBy")]
    public User? Proposer { get; set; }
    
    [ForeignKey("ProposedTo")]
    public User? Receiver { get; set; }
    
    [ForeignKey("ChatId")]
    public Chat? Chat { get; set; }
}
