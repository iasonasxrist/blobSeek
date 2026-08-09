using BlobSeek.Application.Billing;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Billing;

public sealed class BillingCheckoutService(
    IOptions<StripeOptions> stripeOptions,
    IPaymentGateway paymentGateway) : IBillingCheckoutService
{
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

        if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey) || !stripeOptions.TryGetPriceId(command.Plan, out var priceId))
        {
            return BillingCheckoutResult.Failed(
                BillingFailure.MissingConfiguration,
                "Stripe is not configured for this plan.");
        }

        try
        {
            var session = await paymentGateway.CreateCheckoutSessionAsync(new PaymentCheckoutSessionRequest(
                command.Plan,
                priceId,
                command.UserId,
                stripeOptions.SuccessUrl,
                stripeOptions.CancelUrl), cancellationToken);

            return string.IsNullOrWhiteSpace(session.Url)
                ? BillingCheckoutResult.Failed(BillingFailure.ProviderFailure, "Stripe did not return a checkout URL.")
                : BillingCheckoutResult.Success(session.Url);
        }
        catch (Exception ex)
        {
            return BillingCheckoutResult.Failed(BillingFailure.ProviderFailure, ex.Message);
        }
    }
}
