namespace BlobSeek.Application.Billing;

public enum BillingFailure
{
    MissingUser,
    MissingIdempotencyKey,
    PaymentRequired,
    MissingConfiguration,
    ProviderFailure
}

public sealed record BillingCheckoutResult(
    bool Succeeded,
    string? Url,
    BillingFailure? Failure,
    string? Message)
{
    public static BillingCheckoutResult Success(string url) => new(true, url, null, null);

    public static BillingCheckoutResult Failed(BillingFailure failure, string message) => new(false, null, failure, message);
}

public sealed record PaymentAccessDecision(
    bool Allowed,
    BillingFailure? Failure,
    string? Message)
{
    public static PaymentAccessDecision Allow() => new(true, null, null);

    public static PaymentAccessDecision Deny(BillingFailure failure, string message) => new(false, failure, message);
}
