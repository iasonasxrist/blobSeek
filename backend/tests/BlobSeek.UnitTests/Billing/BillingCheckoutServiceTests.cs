using BlobSeek.Application.Billing;
using BlobSeek.Infrastructure.Billing;
using Microsoft.Extensions.Options;

namespace BlobSeek.UnitTests.Billing;

public sealed class BillingCheckoutServiceTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_RejectsMissingUser_WithoutCallingPaymentProvider()
    {
        var gateway = new RecordingPaymentGateway();
        var service = CreateService(gateway);

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Pro, ""),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.MissingUser, result.Failure);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_RejectsMissingPlanPrice_WithoutCallingPaymentProvider()
    {
        var gateway = new RecordingPaymentGateway();
        var service = CreateService(gateway, prices: new Dictionary<string, string>
        {
            ["team"] = "price_team"
        });

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.MissingConfiguration, result.Failure);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_UsesConfiguredPriceForSelectedPlan()
    {
        var gateway = new RecordingPaymentGateway();
        var service = CreateService(gateway, prices: new Dictionary<string, string>
        {
            ["pro"] = "price_pro",
            ["team"] = "price_team"
        });

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Team, "user_123"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("https://checkout.example/session", result.Url);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(BillingPlan.Team, gateway.LastRequest?.Plan);
        Assert.Equal("price_team", gateway.LastRequest?.PriceId);
        Assert.Equal("user_123", gateway.LastRequest?.UserId);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsProviderFailure_WhenProviderDoesNotReturnUrl()
    {
        var gateway = new RecordingPaymentGateway(url: "");
        var service = CreateService(gateway);

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.ProviderFailure, result.Failure);
    }

    private static BillingCheckoutService CreateService(
        RecordingPaymentGateway gateway,
        Dictionary<string, string>? prices = null)
    {
        return new BillingCheckoutService(Options.Create(new StripeOptions
        {
            SecretKey = "sk_test",
            Prices = prices ?? new Dictionary<string, string>
            {
                ["pro"] = "price_pro"
            },
            SuccessUrl = "https://app.example/success",
            CancelUrl = "https://app.example/cancel"
        }), gateway);
    }

    private sealed class RecordingPaymentGateway(string url = "https://checkout.example/session") : IPaymentGateway
    {
        public int Calls { get; private set; }

        public PaymentCheckoutSessionRequest? LastRequest { get; private set; }

        public Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
            PaymentCheckoutSessionRequest request,
            CancellationToken cancellationToken)
        {
            Calls += 1;
            LastRequest = request;
            return Task.FromResult(new PaymentCheckoutSession(url));
        }
    }
}
