using BlobSeek.Domain;
using BlobSeek.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;

namespace BlobSeek.Infrastructure.Persistence;

public sealed class BlobSeekDbContext(DbContextOptions<BlobSeekDbContext> options) : DbContext(options)
{
    public DbSet<BlobIndexState> BlobStates => Set<BlobIndexState>();

    public DbSet<IndexingRun> IndexingRuns => Set<IndexingRun>();

    public DbSet<IndexerLease> IndexerLeases => Set<IndexerLease>();

    public DbSet<BillingCheckoutSession> BillingCheckoutSessions => Set<BillingCheckoutSession>();

    public DbSet<BillingEntitlement> BillingEntitlements => Set<BillingEntitlement>();

    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();

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

        modelBuilder.Entity<BillingCheckoutSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.Plan, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.ProviderIdempotencyKey).IsUnique();
            entity.Property(x => x.UserId).HasMaxLength(128);
            entity.Property(x => x.Plan).HasMaxLength(32);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.Property(x => x.PriceId).HasMaxLength(128);
            entity.Property(x => x.ProviderIdempotencyKey).HasMaxLength(320);
            entity.Property(x => x.ProviderSessionId).HasMaxLength(128);
            entity.Property(x => x.CheckoutUrl).HasMaxLength(2048);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.FailureMessage).HasMaxLength(1024);
        });

        modelBuilder.Entity<BillingEntitlement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.StripeSubscriptionId).IsUnique();
            entity.Property(x => x.UserId).HasMaxLength(128);
            entity.Property(x => x.Plan).HasMaxLength(32);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.StripeCustomerId).HasMaxLength(128);
            entity.Property(x => x.StripeSubscriptionId).HasMaxLength(128);
        });

        modelBuilder.Entity<StripeWebhookEvent>(entity =>
        {
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasMaxLength(128);
            entity.Property(x => x.Type).HasMaxLength(128);
        });
    }
}
