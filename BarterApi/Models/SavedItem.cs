using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public class SavedItem
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string ItemId { get; set; } = string.Empty;
    
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    [ForeignKey("UserId")]
    public User? User { get; set; }
    
    [ForeignKey("ItemId")]
    public Item? Item { get; set; }
}
