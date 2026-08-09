namespace BlobSeek.Application.Billing;

public interface IBillingCheckoutService
{
    Task<BillingCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken);
}

public interface IPaymentAccessService
{
    Task<PaymentAccessDecision> AuthorizeAsync(
        BillingFeature feature,
        string? userId,
        CancellationToken cancellationToken);
}

public interface IPaymentGateway
{
    Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
        PaymentCheckoutSessionRequest request,
        CancellationToken cancellationToken);
}
