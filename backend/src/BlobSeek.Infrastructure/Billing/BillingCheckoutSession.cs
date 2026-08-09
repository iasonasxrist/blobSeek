using BlobSeek.Application.Billing;

namespace BlobSeek.Infrastructure.Billing;

public sealed class BillingCheckoutSession
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    public string Plan { get; set; } = "";

    public string IdempotencyKey { get; set; } = "";

    public string PriceId { get; set; } = "";

    public string ProviderIdempotencyKey { get; set; } = "";

    public string? ProviderSessionId { get; set; }

    public string? CheckoutUrl { get; set; }

    public string Status { get; set; } = BillingCheckoutStatus.Pending;

    public string? FailureMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public static BillingCheckoutSession CreatePending(
        string userId,
        BillingPlan plan,
        string idempotencyKey,
        string priceId,
        string providerIdempotencyKey,
        DateTimeOffset now)
    {
        return new BillingCheckoutSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = plan.ToConfigurationKey(),
            IdempotencyKey = idempotencyKey,
            PriceId = priceId,
            ProviderIdempotencyKey = providerIdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

public static class BillingCheckoutStatus
{
    public const string Pending = "pending";

    public const string Succeeded = "succeeded";

    public const string Failed = "failed";
}
