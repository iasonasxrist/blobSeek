using BlobSeek.Application.Billing;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Billing;

public sealed class ConfiguredPaymentAccessService(
    IOptions<BillingOptions> options,
    IBillingEntitlementStore entitlements) : IPaymentAccessService
{
    private readonly BillingOptions options = options.Value;

    public async Task<PaymentAccessDecision> AuthorizeAsync(
        BillingFeature feature,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!options.RequireActiveSubscription)
        {
            return PaymentAccessDecision.Allow();
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return PaymentAccessDecision.Deny(
                BillingFailure.MissingUser,
                "Sign in before using paid BlobSeek features.");
        }

        if (options.PaidUserIds.Contains(userId) || await entitlements.HasActiveEntitlementAsync(userId, cancellationToken))
        {
            return PaymentAccessDecision.Allow();
        }

        return PaymentAccessDecision.Deny(
            BillingFailure.PaymentRequired,
            $"An active subscription is required to use {feature}.");
    }
}
