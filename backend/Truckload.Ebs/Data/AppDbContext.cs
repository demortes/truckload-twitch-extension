using Microsoft.EntityFrameworkCore;
using Truckload.Ebs.Entities;

namespace Truckload.Ebs.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<TelemetrySnapshot> TelemetrySnapshots => Set<TelemetrySnapshot>();

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
    }
}
