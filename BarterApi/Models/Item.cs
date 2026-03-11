using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarterApi.Models;

public enum ItemCategory
{
    Electronics,
    Clothing,
    Books,
    Furniture,
    Sports,
    Other,
    Service
}

public enum ItemCondition
{
    New,
    LikeNew,
    Good,
    Fair,
    Poor
}

public enum ItemType
{
    Product,
    Service
}

public class Item
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string OwnerId { get; set; } = string.Empty;
    
    [Required]
    public string OwnerName { get; set; } = string.Empty;
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    // Stored as comma-separated URLs
    public string ImageUrlsJson { get; set; } = "[]";
    
    public ItemCategory Category { get; set; } = ItemCategory.Other;
    public ItemCondition Condition { get; set; } = ItemCondition.Good;
    
    public string? PreferredExchange { get; set; }
    
    [Required]
    public string Location { get; set; } = string.Empty;
    
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? DetailedAddress { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsAvailable { get; set; } = true;
    public bool IsExchanged { get; set; } = false;
    
    public ItemType ItemType { get; set; } = ItemType.Product;
    public bool IsRemote { get; set; } = false;
    
    // Navigation
    [ForeignKey("OwnerId")]
    public User? Owner { get; set; }
    
    public ICollection<SavedItem> SavedByUsers { get; set; } = new List<SavedItem>();
}
