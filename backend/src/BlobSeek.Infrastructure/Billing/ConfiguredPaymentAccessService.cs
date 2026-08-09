using BlobSeek.Application.Billing;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Billing;

public sealed class ConfiguredPaymentAccessService(IOptions<BillingOptions> options) : IPaymentAccessService
{
    private readonly BillingOptions options = options.Value;

    public Task<PaymentAccessDecision> AuthorizeAsync(
        BillingFeature feature,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!options.RequireActiveSubscription)
        {
            return Task.FromResult(PaymentAccessDecision.Allow());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(PaymentAccessDecision.Deny(
                BillingFailure.MissingUser,
                "Sign in before using paid BlobSeek features."));
        }

        return Task.FromResult(options.PaidUserIds.Contains(userId)
            ? PaymentAccessDecision.Allow()
            : PaymentAccessDecision.Deny(
                BillingFailure.PaymentRequired,
                $"An active subscription is required to use {feature}."));
    }
}
