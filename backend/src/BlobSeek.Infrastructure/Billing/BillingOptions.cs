using BlobSeek.Application.Billing;

namespace BlobSeek.Infrastructure.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public bool RequireActiveSubscription { get; set; }

    public HashSet<string> PaidUserIds { get; set; } = new(StringComparer.Ordinal);
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = "";

    public Dictionary<string, string> Prices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string SuccessUrl { get; set; } = "http://localhost:8000/search?checkout=success&session_id={CHECKOUT_SESSION_ID}";

    public string CancelUrl { get; set; } = "http://localhost:8000/?checkout=cancelled";

    public bool TryGetPriceId(BillingPlan plan, out string priceId)
    {
        return Prices.TryGetValue(plan.ToConfigurationKey(), out priceId!) && !string.IsNullOrWhiteSpace(priceId);
    }
}
