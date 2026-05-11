using Market.Entity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Market.Implimitation.Interfaces;

namespace Market.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users { get; set; } = null!;
    public DbSet<ProductEntity> Products { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<EmailVerificationCodeEntity> EmailVerificationCodeEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>()
            .HasQueryFilter(x => !x.IsDeleted);

        modelBuilder.Entity<ProductEntity>()
            .HasQueryFilter(x => !x.IsDeleted);
    }

    public override int SaveChanges()
    {
        ApplySoftDelete();
        AddAuditLogs();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDelete();
        AddAuditLogs();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplySoftDelete()
    {
        var deletedEntries = ChangeTracker.Entries<ISoftDelete>()
            .Where(x => x.State == EntityState.Deleted);

        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.DeletedBy ??= "system";
        }
    }

    private void AddAuditLogs() 
    {
        var auditEntries = new List<AuditLog>();

        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added
                     || e.State == EntityState.Modified
                     || e.State == EntityState.Deleted)
            .Where(e => e.Entity is not AuditLog);

        foreach (var entry in entries)
        {
            var audit = new AuditLog
            {
                UserId = "system",
                Action = entry.State.ToString(),
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "0",
                CreatedAt = DateTime.UtcNow,
                OldValues = JsonSerializer.Serialize(entry.OriginalValues.ToObject()),
                NewValues = JsonSerializer.Serialize(entry.CurrentValues.ToObject())
            };

            auditEntries.Add(audit);
        }

        if (auditEntries.Count > 0)
            AuditLogs.AddRange(auditEntries);
    }
}