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
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "", "idem_1"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.MissingUser, result.Failure);
        Assert.Equal(0, gateway.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCheckoutSessionAsync_RejectsMissingIdempotencyKey_WithoutCallingPaymentProvider(string idempotencyKey)
    {
        var gateway = new RecordingPaymentGateway();
        var service = CreateService(gateway);

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123", idempotencyKey),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.MissingIdempotencyKey, result.Failure);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_RejectsOverlongIdempotencyKey_WithoutCallingPaymentProvider()
    {
        var gateway = new RecordingPaymentGateway();
        var service = CreateService(gateway);

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123", new string('x', 121)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.MissingIdempotencyKey, result.Failure);
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
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123", "idem_1"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.MissingConfiguration, result.Failure);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_UsesConfiguredPriceAndProviderIdempotencyKeyForSelectedPlan()
    {
        var gateway = new RecordingPaymentGateway();
        var service = CreateService(gateway, prices: new Dictionary<string, string>
        {
            ["pro"] = "price_pro",
            ["team"] = "price_team"
        });

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Team, "user_123", "idem_1"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("https://checkout.example/session", result.Url);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(BillingPlan.Team, gateway.LastRequest?.Plan);
        Assert.Equal("price_team", gateway.LastRequest?.PriceId);
        Assert.Equal("user_123", gateway.LastRequest?.UserId);
        Assert.Contains("user_123", gateway.LastRequest?.IdempotencyKey);
        Assert.Contains("team", gateway.LastRequest?.IdempotencyKey);
        Assert.Contains("idem_1", gateway.LastRequest?.IdempotencyKey);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReplaysExistingCheckoutUrl_ForDuplicateIdempotencyKey()
    {
        var gateway = new RecordingPaymentGateway();
        var store = new InMemoryCheckoutSessionStore();
        var service = CreateService(gateway, store: store);
        var command = new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123", "idem_replay");

        var first = await service.CreateCheckoutSessionAsync(command, CancellationToken.None);
        var second = await service.CreateCheckoutSessionAsync(command, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Url, second.Url);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_CreatesSingleProviderSession_ForConcurrentDuplicateRequests()
    {
        var gateway = new RecordingPaymentGateway(delay: TimeSpan.FromMilliseconds(30));
        var store = new InMemoryCheckoutSessionStore();
        var service = CreateService(gateway, store: store);
        var command = new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123", "idem_concurrent");

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            service.CreateCheckoutSessionAsync(command, CancellationToken.None)));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results.Select(x => x.Url).Distinct());
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsProviderFailure_AndPersistsFailure_WhenProviderDoesNotReturnUrl()
    {
        var gateway = new RecordingPaymentGateway(url: "");
        var store = new InMemoryCheckoutSessionStore();
        var service = CreateService(gateway, store: store);

        var result = await service.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionCommand(BillingPlan.Pro, "user_123", "idem_failure"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingFailure.ProviderFailure, result.Failure);
        Assert.Equal(BillingCheckoutStatus.Failed, Assert.Single(store.Sessions).Status);
    }

    private static BillingCheckoutService CreateService(
        RecordingPaymentGateway gateway,
        Dictionary<string, string>? prices = null,
        ICheckoutSessionStore? store = null)
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
        }), store ?? new InMemoryCheckoutSessionStore(), gateway, TimeProvider.System);
    }

    private sealed class RecordingPaymentGateway(
        string url = "https://checkout.example/session",
        TimeSpan? delay = null) : IPaymentGateway
    {
        private int calls;

        public int Calls => calls;

        public PaymentCheckoutSessionRequest? LastRequest { get; private set; }

        public async Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
            PaymentCheckoutSessionRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            LastRequest = request;
            if (delay is not null)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }

            return new PaymentCheckoutSession(url, "cs_test_123");
        }
    }

    private sealed class InMemoryCheckoutSessionStore : ICheckoutSessionStore
    {
        private readonly object gate = new();
        private readonly Dictionary<string, MutableSession> sessions = new(StringComparer.Ordinal);

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return sessions.Count;
                }
            }
        }

        public IReadOnlyList<StoredCheckoutSession> Sessions
        {
            get
            {
                lock (gate)
                {
                    return sessions.Values.Select(ToStored).ToList();
                }
            }
        }

        public Task<StoredCheckoutSession?> FindAsync(
            string userId,
            BillingPlan plan,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                return Task.FromResult(sessions.TryGetValue(Key(userId, plan, idempotencyKey), out var session)
                    ? ToStored(session)
                    : null);
            }
        }

        public Task<StoredCheckoutSession> CreatePendingAsync(
            string userId,
            BillingPlan plan,
            string idempotencyKey,
            string priceId,
            string providerIdempotencyKey,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var key = Key(userId, plan, idempotencyKey);
                if (!sessions.TryGetValue(key, out var session))
                {
                    session = new MutableSession(
                        Guid.NewGuid(),
                        userId,
                        plan,
                        idempotencyKey,
                        priceId,
                        providerIdempotencyKey,
                        BillingCheckoutStatus.Pending,
                        null,
                        null);
                    sessions.Add(key, session);
                }

                return Task.FromResult(ToStored(session));
            }
        }

        public Task MarkSucceededAsync(
            Guid checkoutSessionId,
            string checkoutUrl,
            string? providerSessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var session = sessions.Values.Single(x => x.Id == checkoutSessionId);
                session.Status = BillingCheckoutStatus.Succeeded;
                session.CheckoutUrl = checkoutUrl;
                session.ProviderSessionId = providerSessionId;
            }

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid checkoutSessionId,
            string failureMessage,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var session = sessions.Values.Single(x => x.Id == checkoutSessionId);
                session.Status = BillingCheckoutStatus.Failed;
            }

            return Task.CompletedTask;
        }

        private static string Key(string userId, BillingPlan plan, string idempotencyKey) =>
            $"{userId}:{plan.ToConfigurationKey()}:{idempotencyKey}";

        private static StoredCheckoutSession ToStored(MutableSession session) => new(
            session.Id,
            session.UserId,
            session.Plan,
            session.IdempotencyKey,
            session.PriceId,
            session.ProviderIdempotencyKey,
            session.Status,
            session.CheckoutUrl,
            session.ProviderSessionId);

        private sealed class MutableSession(
            Guid id,
            string userId,
            BillingPlan plan,
            string idempotencyKey,
            string priceId,
            string providerIdempotencyKey,
            string status,
            string? checkoutUrl,
            string? providerSessionId)
        {
            public Guid Id { get; } = id;

            public string UserId { get; } = userId;

            public BillingPlan Plan { get; } = plan;

            public string IdempotencyKey { get; } = idempotencyKey;

            public string PriceId { get; } = priceId;

            public string ProviderIdempotencyKey { get; } = providerIdempotencyKey;

            public string Status { get; set; } = status;

            public string? CheckoutUrl { get; set; } = checkoutUrl;

            public string? ProviderSessionId { get; set; } = providerSessionId;
        }
    }
}
