using BlobSeek.Application.Billing;
using BlobSeek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlobSeek.Infrastructure.Billing;

public interface ICheckoutSessionStore
{
    Task<StoredCheckoutSession?> FindAsync(
        string userId,
        BillingPlan plan,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StoredCheckoutSession> CreatePendingAsync(
        string userId,
        BillingPlan plan,
        string idempotencyKey,
        string priceId,
        string providerIdempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkSucceededAsync(
        Guid checkoutSessionId,
        string checkoutUrl,
        string? providerSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid checkoutSessionId,
        string failureMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed record StoredCheckoutSession(
    Guid Id,
    string UserId,
    BillingPlan Plan,
    string IdempotencyKey,
    string PriceId,
    string ProviderIdempotencyKey,
    string Status,
    string? CheckoutUrl,
    string? ProviderSessionId);

public sealed class EfCheckoutSessionStore(BlobSeekDbContext db) : ICheckoutSessionStore
{
    public async Task<StoredCheckoutSession?> FindAsync(
        string userId,
        BillingPlan plan,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var planKey = plan.ToConfigurationKey();
        var session = await db.BillingCheckoutSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Plan == planKey &&
                x.IdempotencyKey == idempotencyKey, cancellationToken);

        return session is null ? null : ToStored(session);
    }

    public async Task<StoredCheckoutSession> CreatePendingAsync(
        string userId,
        BillingPlan plan,
        string idempotencyKey,
        string priceId,
        string providerIdempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = BillingCheckoutSession.CreatePending(userId, plan, idempotencyKey, priceId, providerIdempotencyKey, now);
        db.BillingCheckoutSessions.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return ToStored(entity);
        }
        catch (DbUpdateException)
        {
            db.Entry(entity).State = EntityState.Detached;
            var existing = await FindAsync(userId, plan, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }
    }

    public async Task MarkSucceededAsync(
        Guid checkoutSessionId,
        string checkoutUrl,
        string? providerSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await db.BillingCheckoutSessions.SingleAsync(x => x.Id == checkoutSessionId, cancellationToken);
        session.Status = BillingCheckoutStatus.Succeeded;
        session.CheckoutUrl = checkoutUrl;
        session.ProviderSessionId = providerSessionId;
        session.UpdatedAt = now;
        session.CompletedAt = now;
        session.FailureMessage = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid checkoutSessionId,
        string failureMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await db.BillingCheckoutSessions.SingleAsync(x => x.Id == checkoutSessionId, cancellationToken);
        session.Status = BillingCheckoutStatus.Failed;
        session.FailureMessage = failureMessage;
        session.UpdatedAt = now;
        session.CompletedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static StoredCheckoutSession ToStored(BillingCheckoutSession session) => new(
        session.Id,
        session.UserId,
        Enum.Parse<BillingPlan>(session.Plan, ignoreCase: true),
        session.IdempotencyKey,
        session.PriceId,
        session.ProviderIdempotencyKey,
        session.Status,
        session.CheckoutUrl,
        session.ProviderSessionId);
}
