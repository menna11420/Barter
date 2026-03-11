using BarterApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BarterApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SavedItem> SavedItems => Set<SavedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // Item → User (owner)
        modelBuilder.Entity<Item>()
            .HasOne(i => i.Owner)
            .WithMany(u => u.Items)
            .HasForeignKey(i => i.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message → Chat
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Chat)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message → User (sender) - no cascade to avoid cycles
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.NoAction);

        // Exchange → Chat
        modelBuilder.Entity<Exchange>()
            .HasOne(e => e.Chat)
            .WithMany(c => c.Exchanges)
            .HasForeignKey(e => e.ChatId)
            .OnDelete(DeleteBehavior.NoAction);

        // Exchange → User (proposer)
        modelBuilder.Entity<Exchange>()
            .HasOne(e => e.Proposer)
            .WithMany()
            .HasForeignKey(e => e.ProposedBy)
            .OnDelete(DeleteBehavior.NoAction);

        // Exchange → User (receiver)
        modelBuilder.Entity<Exchange>()
            .HasOne(e => e.Receiver)
            .WithMany()
            .HasForeignKey(e => e.ProposedTo)
            .OnDelete(DeleteBehavior.NoAction);

        // Notification → User
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Review → Reviewer
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewsGiven)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Review → Reviewee
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Reviewee)
            .WithMany(u => u.ReviewsReceived)
            .HasForeignKey(r => r.RevieweeId)
            .OnDelete(DeleteBehavior.NoAction);

        // SavedItem → User
        modelBuilder.Entity<SavedItem>()
            .HasOne(s => s.User)
            .WithMany(u => u.SavedItems)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // SavedItem → Item
        modelBuilder.Entity<SavedItem>()
            .HasOne(s => s.Item)
            .WithMany(i => i.SavedByUsers)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one save per user per item
        modelBuilder.Entity<SavedItem>()
            .HasIndex(s => new { s.UserId, s.ItemId })
            .IsUnique();
    }
}
