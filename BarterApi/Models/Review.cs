using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public class Review
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string ReviewerId { get; set; } = string.Empty;
    
    [Required]
    public string RevieweeId { get; set; } = string.Empty;
    
    [Required]
    public string ExchangeId { get; set; } = string.Empty;
    
    [Range(1, 5)]
    public double Rating { get; set; }
    
    public string Comment { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    [ForeignKey("ReviewerId")]
    public User? Reviewer { get; set; }
    
    [ForeignKey("RevieweeId")]
    public User? Reviewee { get; set; }
}
