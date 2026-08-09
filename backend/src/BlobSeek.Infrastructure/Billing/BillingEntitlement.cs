namespace BlobSeek.Infrastructure.Billing;

public sealed class BillingEntitlement
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    public string Plan { get; set; } = "";

    public string Status { get; set; } = BillingEntitlementStatus.Active;

    public string? StripeCustomerId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public static class BillingEntitlementStatus
{
    public const string Active = "active";

    public const string Trialing = "trialing";

    public const string PastDue = "past_due";

    public const string Canceled = "canceled";
}
