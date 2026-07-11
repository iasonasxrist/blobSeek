using BlobSeek.Domain;
using Microsoft.EntityFrameworkCore;

namespace BlobSeek.Infrastructure.Persistence;

public sealed class BlobSeekDbContext(DbContextOptions<BlobSeekDbContext> options) : DbContext(options)
{
    public DbSet<BlobIndexState> BlobStates => Set<BlobIndexState>();

    public DbSet<IndexingRun> IndexingRuns => Set<IndexingRun>();

    public DbSet<IndexerLease> IndexerLeases => Set<IndexerLease>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobIndexState>(entity =>
        {
            entity.HasKey(x => x.DocumentId);
            entity.HasIndex(x => new { x.StorageAccount, x.ContainerName, x.BlobName }).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.DocumentId).HasMaxLength(64);
            entity.Property(x => x.StorageAccount).HasMaxLength(128);
            entity.Property(x => x.ContainerName).HasMaxLength(256);
            entity.Property(x => x.ETag).HasMaxLength(256);
            entity.Property(x => x.ExtractorVersion).HasMaxLength(64);
        });

        modelBuilder.Entity<IndexingRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.StartedAt);
            entity.Property(x => x.Status).HasMaxLength(32);
        });

        modelBuilder.Entity<IndexerLease>(entity =>
        {
            entity.HasKey(x => x.Scope);
            entity.Property(x => x.Scope).HasMaxLength(256);
            entity.Property(x => x.OwnerId).HasMaxLength(128);
        });
    }
}
