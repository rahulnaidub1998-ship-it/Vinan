using Microsoft.EntityFrameworkCore;
using Vinan.Api.Models;

namespace Vinan.Api.Data;

public sealed class VinanDbContext : DbContext
{
    public VinanDbContext(DbContextOptions<VinanDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemoryItem> Memories => Set<MemoryItem>();
    public DbSet<ReminderItem> Reminders => Set<ReminderItem>();
    public DbSet<PendingAction> PendingActions => Set<PendingAction>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ConversationSession> Conversations => Set<ConversationSession>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemoryItem>().Property(item => item.Text).HasMaxLength(2000);
        modelBuilder.Entity<MemoryItem>().Property(item => item.Category).HasMaxLength(80);
        modelBuilder.Entity<MemoryItem>().Property(item => item.CreatedAt).HasConversion<long>();

        modelBuilder.Entity<ReminderItem>().Property(item => item.Title).HasMaxLength(500);
        modelBuilder.Entity<ReminderItem>().Property(item => item.When).HasMaxLength(120);
        modelBuilder.Entity<ReminderItem>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<ReminderItem>().HasIndex(item => new { item.IsComplete, item.CreatedAt });

        modelBuilder.Entity<PendingAction>().Property(item => item.Summary).HasMaxLength(3000);
        modelBuilder.Entity<PendingAction>().Property(item => item.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<PendingAction>().Property(item => item.Status).HasConversion<string>();
        modelBuilder.Entity<PendingAction>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<PendingAction>().HasIndex(item => new { item.Status, item.CreatedAt });

        modelBuilder.Entity<AuditEvent>().Property(item => item.Action).HasMaxLength(3000);
        modelBuilder.Entity<AuditEvent>().Property(item => item.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<AuditEvent>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<AuditEvent>().HasIndex(item => item.CreatedAt);

        modelBuilder.Entity<ConversationSession>().Property(item => item.Title).HasMaxLength(120);
        modelBuilder.Entity<ConversationSession>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<ConversationSession>().Property(item => item.UpdatedAt).HasConversion<long>();
        modelBuilder.Entity<ConversationSession>().HasIndex(item => item.UpdatedAt);

        modelBuilder.Entity<ConversationMessage>().Property(item => item.Role).HasMaxLength(20);
        modelBuilder.Entity<ConversationMessage>().Property(item => item.Provider).HasMaxLength(80);
        modelBuilder.Entity<ConversationMessage>().Property(item => item.Text).HasMaxLength(20000);
        modelBuilder.Entity<ConversationMessage>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<ConversationMessage>().HasIndex(item => new { item.ConversationId, item.CreatedAt });
    }
}
