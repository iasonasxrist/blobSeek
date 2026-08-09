using BlobSeek.Application.Billing;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Billing;

public sealed class BillingCheckoutService(
    IOptions<StripeOptions> stripeOptions,
    ICheckoutSessionStore checkoutSessions,
    IPaymentGateway paymentGateway,
    TimeProvider clock) : IBillingCheckoutService
{
    private const int MaxIdempotencyKeyLength = 120;
    private readonly StripeOptions stripeOptions = stripeOptions.Value;

    public async Task<BillingCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return BillingCheckoutResult.Failed(
                BillingFailure.MissingUser,
                "Sign in before starting checkout.");
        }

        var idempotencyKey = NormalizeIdempotencyKey(command.IdempotencyKey);
        if (idempotencyKey is null)
        {
            return BillingCheckoutResult.Failed(
                BillingFailure.MissingIdempotencyKey,
                "Checkout requires an idempotency key.");
        }

        if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey) || !stripeOptions.TryGetPriceId(command.Plan, out var priceId))
        {
            return BillingCheckoutResult.Failed(
                BillingFailure.MissingConfiguration,
                "Stripe is not configured for this plan.");
        }

        var lockKey = $"{command.UserId}:{command.Plan.ToConfigurationKey()}:{idempotencyKey}";
        await using var checkoutLock = await CheckoutKeyedLocks.AcquireAsync(lockKey, cancellationToken);

        var existing = await checkoutSessions.FindAsync(command.UserId, command.Plan, idempotencyKey, cancellationToken);
        if (existing?.CheckoutUrl is not null && existing.Status == BillingCheckoutStatus.Succeeded)
        {
            return BillingCheckoutResult.Success(existing.CheckoutUrl);
        }

        var now = clock.GetUtcNow();
        var providerIdempotencyKey = BuildProviderIdempotencyKey(command.UserId, command.Plan, idempotencyKey);
        var reservation = existing ?? await checkoutSessions.CreatePendingAsync(
            command.UserId,
            command.Plan,
            idempotencyKey,
            priceId,
            providerIdempotencyKey,
            now,
            cancellationToken);

        if (reservation.CheckoutUrl is not null && reservation.Status == BillingCheckoutStatus.Succeeded)
        {
            return BillingCheckoutResult.Success(reservation.CheckoutUrl);
        }

        try
        {
            var session = await paymentGateway.CreateCheckoutSessionAsync(new PaymentCheckoutSessionRequest(
                command.Plan,
                priceId,
                command.UserId,
                stripeOptions.SuccessUrl,
                stripeOptions.CancelUrl,
                providerIdempotencyKey), cancellationToken);

            if (string.IsNullOrWhiteSpace(session.Url))
            {
                await checkoutSessions.MarkFailedAsync(reservation.Id, "Stripe did not return a checkout URL.", clock.GetUtcNow(), cancellationToken);
                return BillingCheckoutResult.Failed(BillingFailure.ProviderFailure, "Stripe did not return a checkout URL.");
            }

            await checkoutSessions.MarkSucceededAsync(reservation.Id, session.Url, session.ProviderSessionId, clock.GetUtcNow(), cancellationToken);
            return BillingCheckoutResult.Success(session.Url);
        }
        catch (Exception ex)
        {
            await checkoutSessions.MarkFailedAsync(reservation.Id, ex.Message, clock.GetUtcNow(), cancellationToken);
            return BillingCheckoutResult.Failed(BillingFailure.ProviderFailure, ex.Message);
        }
    }

    private static string? NormalizeIdempotencyKey(string idempotencyKey)
    {
        var normalized = idempotencyKey.Trim();
        if (normalized.Length is 0 or > MaxIdempotencyKeyLength)
        {
            return null;
        }

        return normalized;
    }

    private static string BuildProviderIdempotencyKey(string userId, BillingPlan plan, string idempotencyKey)
    {
        return $"blobseek-checkout:{userId}:{plan.ToConfigurationKey()}:{idempotencyKey}";
    }
}
