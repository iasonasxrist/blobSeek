namespace BlobSeek.Application.Billing;

public sealed record CheckoutRequest(BillingPlan Plan, string IdempotencyKey);

public sealed record CheckoutSessionResponse(string Url);

public sealed record CreateCheckoutSessionCommand(BillingPlan Plan, string UserId, string IdempotencyKey);

public sealed record PaymentCheckoutSessionRequest(
    BillingPlan Plan,
    string PriceId,
    string UserId,
    string SuccessUrl,
    string CancelUrl,
    string IdempotencyKey);

public sealed record PaymentCheckoutSession(string Url, string? ProviderSessionId = null);
