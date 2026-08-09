using BlobSeek.Application.Billing;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace BlobSeek.Infrastructure.Billing;

public sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeOptions options = options.Value;

    public async Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
        PaymentCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        StripeConfiguration.ApiKey = options.SecretKey;

        var service = new SessionService();
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = request.UserId,
            AllowPromotionCodes = true,
            AutomaticTax = new SessionAutomaticTaxOptions { Enabled = true },
            TaxIdCollection = new SessionTaxIdCollectionOptions { Enabled = true },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = request.PriceId,
                    Quantity = 1
                }
            },
            Metadata = MetadataFor(request),
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = MetadataFor(request)
            }
        }, new RequestOptions
        {
            IdempotencyKey = request.IdempotencyKey
        }, cancellationToken: cancellationToken);

        return new PaymentCheckoutSession(session.Url, session.Id);
    }

    private static Dictionary<string, string> MetadataFor(PaymentCheckoutSessionRequest request) => new()
    {
        ["clerk_user_id"] = request.UserId,
        ["plan"] = request.Plan.ToConfigurationKey()
    };
}
