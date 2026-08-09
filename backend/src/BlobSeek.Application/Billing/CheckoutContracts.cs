namespace BlobSeek.Application.Billing;

public sealed record CheckoutRequest(BillingPlan Plan);

public sealed record CheckoutSessionResponse(string Url);

public sealed record CreateCheckoutSessionCommand(BillingPlan Plan, string UserId);

public sealed record PaymentCheckoutSessionRequest(
    BillingPlan Plan,
    string PriceId,
    string UserId,
    string SuccessUrl,
    string CancelUrl);

public sealed record PaymentCheckoutSession(string Url);
