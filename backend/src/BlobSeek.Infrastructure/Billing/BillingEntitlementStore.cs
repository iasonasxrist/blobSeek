using BlobSeek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlobSeek.Infrastructure.Billing;

public interface IBillingEntitlementStore
{
    Task<bool> HasActiveEntitlementAsync(string userId, CancellationToken cancellationToken);
}

public sealed class EfBillingEntitlementStore(BlobSeekDbContext db) : IBillingEntitlementStore
{
    public Task<bool> HasActiveEntitlementAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return db.BillingEntitlements.AsNoTracking().AnyAsync(x =>
            x.UserId == userId &&
            (x.Status == BillingEntitlementStatus.Active || x.Status == BillingEntitlementStatus.Trialing) &&
            (x.CurrentPeriodEnd == null || x.CurrentPeriodEnd > now), cancellationToken);
    }
}
