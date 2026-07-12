using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vinan.Api.Models;
using Vinan.Api.Security;

namespace Vinan.Api.Data;

public sealed class VinanDbContext : DbContext
{
    private readonly PersonalDataProtector _personalData;

    public VinanDbContext(DbContextOptions<VinanDbContext> options, PersonalDataProtector personalData)
        : base(options)
    {
        _personalData = personalData;
    }

    public DbSet<MemoryItem> Memories => Set<MemoryItem>();
    public DbSet<ReminderItem> Reminders => Set<ReminderItem>();
    public DbSet<PendingAction> PendingActions => Set<PendingAction>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ConversationSession> Conversations => Set<ConversationSession>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<OwnerProfile> OwnerProfiles => Set<OwnerProfile>();
    public DbSet<DeviceEnrollment> DeviceEnrollments => Set<DeviceEnrollment>();
    public DbSet<NoteItem> Notes => Set<NoteItem>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ProviderCredential> ProviderCredentials => Set<ProviderCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var protectedText = new ValueConverter<string, string>(
            value => _personalData.Protect(value),
            value => _personalData.Unprotect(value));

        modelBuilder.Entity<MemoryItem>().Property(item => item.Text).HasMaxLength(4000).HasConversion(protectedText);
        modelBuilder.Entity<MemoryItem>().Property(item => item.Category).HasMaxLength(80);
        modelBuilder.Entity<MemoryItem>().Property(item => item.CreatedAt).HasConversion<long>();

        modelBuilder.Entity<ReminderItem>().Property(item => item.Title).HasMaxLength(1200).HasConversion(protectedText);
        modelBuilder.Entity<ReminderItem>().Property(item => item.When).HasMaxLength(500).HasConversion(protectedText);
        modelBuilder.Entity<ReminderItem>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<ReminderItem>().HasIndex(item => new { item.IsComplete, item.CreatedAt });

        modelBuilder.Entity<PendingAction>().Property(item => item.Summary).HasMaxLength(6000).HasConversion(protectedText);
        modelBuilder.Entity<PendingAction>().Property(item => item.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<PendingAction>().Property(item => item.Status).HasConversion<string>();
        modelBuilder.Entity<PendingAction>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<PendingAction>().HasIndex(item => new { item.Status, item.CreatedAt });

        modelBuilder.Entity<AuditEvent>().Property(item => item.Action).HasMaxLength(6000).HasConversion(protectedText);
        modelBuilder.Entity<AuditEvent>().Property(item => item.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<AuditEvent>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<AuditEvent>().HasIndex(item => item.CreatedAt);

        modelBuilder.Entity<ConversationSession>().Property(item => item.Title).HasMaxLength(600).HasConversion(protectedText);
        modelBuilder.Entity<ConversationSession>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<ConversationSession>().Property(item => item.UpdatedAt).HasConversion<long>();
        modelBuilder.Entity<ConversationSession>().HasIndex(item => item.UpdatedAt);

        modelBuilder.Entity<ConversationMessage>().Property(item => item.Role).HasMaxLength(20);
        modelBuilder.Entity<ConversationMessage>().Property(item => item.Provider).HasMaxLength(80);
        modelBuilder.Entity<ConversationMessage>().Property(item => item.Text).HasMaxLength(30000).HasConversion(protectedText);
        modelBuilder.Entity<ConversationMessage>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<ConversationMessage>().HasIndex(item => new { item.ConversationId, item.CreatedAt });

        modelBuilder.Entity<OwnerProfile>().Property(item => item.DisplayName).HasMaxLength(500).HasConversion(protectedText);
        modelBuilder.Entity<OwnerProfile>().Property(item => item.Scope).HasMaxLength(20);
        modelBuilder.Entity<OwnerProfile>().Property(item => item.PasswordHash).HasMaxLength(1000);
        modelBuilder.Entity<OwnerProfile>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<OwnerProfile>().HasIndex(item => item.Scope).IsUnique();

        modelBuilder.Entity<DeviceEnrollment>().Property(item => item.Name).HasMaxLength(1000).HasConversion(protectedText);
        modelBuilder.Entity<DeviceEnrollment>().Property(item => item.EnrolledAt).HasConversion<long>();
        modelBuilder.Entity<DeviceEnrollment>().Property(item => item.LastSeenAt).HasConversion<long>();
        modelBuilder.Entity<DeviceEnrollment>().HasIndex(item => new { item.OwnerId, item.RevokedAt });
        modelBuilder.Entity<DeviceEnrollment>()
            .HasOne<OwnerProfile>()
            .WithMany()
            .HasForeignKey(item => item.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NoteItem>().Property(item => item.Text).HasMaxLength(10000).HasConversion(protectedText);
        modelBuilder.Entity<NoteItem>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<NoteItem>().Property(item => item.UpdatedAt).HasConversion<long>();
        modelBuilder.Entity<NoteItem>().HasIndex(item => item.UpdatedAt);

        modelBuilder.Entity<TaskItem>().Property(item => item.Title).HasMaxLength(2000).HasConversion(protectedText);
        modelBuilder.Entity<TaskItem>().Property(item => item.DueAt).HasConversion<long?>();
        modelBuilder.Entity<TaskItem>().Property(item => item.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<TaskItem>().Property(item => item.CompletedAt).HasConversion<long?>();
        modelBuilder.Entity<TaskItem>().HasIndex(item => new { item.IsComplete, item.DueAt, item.Priority });

        modelBuilder.Entity<ProviderCredential>().Property(item => item.Provider).HasMaxLength(40);
        modelBuilder.Entity<ProviderCredential>().Property(item => item.Secret).HasMaxLength(4000).HasConversion(protectedText);
        modelBuilder.Entity<ProviderCredential>().Property(item => item.Model).HasMaxLength(120);
        modelBuilder.Entity<ProviderCredential>().Property(item => item.UpdatedAt).HasConversion<long>();
        modelBuilder.Entity<ProviderCredential>().HasIndex(item => item.Provider).IsUnique();
    }
}
