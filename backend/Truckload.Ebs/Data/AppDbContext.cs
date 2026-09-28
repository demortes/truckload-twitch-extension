using Microsoft.EntityFrameworkCore;
using Truckload.Ebs.Entities;

namespace Truckload.Ebs.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<TelemetrySnapshot> TelemetrySnapshots => Set<TelemetrySnapshot>();
    public DbSet<JobHistoryEntry> JobHistoryEntries => Set<JobHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(c => c.ChannelId);
            entity.Property(c => c.ChannelId).HasMaxLength(64);
            entity.Property(c => c.IngestKey).HasMaxLength(128);
            entity.HasIndex(c => c.IngestKey).IsUnique();
        });

        modelBuilder.Entity<TelemetrySnapshot>(entity =>
        {
            entity.HasKey(t => t.ChannelId);
            // "jsonb" is Postgres-specific; guard it so the SQLite provider used by tests can build the model.
            if (Database.IsNpgsql())
                entity.Property(t => t.PayloadJson).HasColumnType("jsonb");
            entity.HasOne(t => t.Channel)
                  .WithOne(c => c.LatestTelemetry)
                  .HasForeignKey<TelemetrySnapshot>(t => t.ChannelId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobHistoryEntry>(entity =>
        {
            entity.HasKey(h => h.Id);
            // Ids are generated in application code (Guid.NewGuid()), not by the database.
            entity.Property(h => h.Id).ValueGeneratedNever();
            // ChannelId is left as plain "text" (no HasMaxLength), matching TelemetrySnapshot's
            // FK column above — only the Channels table's own primary key is varchar(64).
            entity.Property(h => h.Cargo).HasMaxLength(64);
            entity.Property(h => h.Source).HasMaxLength(64);
            entity.Property(h => h.Destination).HasMaxLength(64);
            entity.HasIndex(h => new { h.ChannelId, h.CompletedAt });
            entity.HasOne(h => h.Channel)
                  .WithMany(c => c.JobHistory)
                  .HasForeignKey(h => h.ChannelId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
