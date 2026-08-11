using BlobSeek.Application.Billing;
using BlobSeek.Infrastructure.Billing;
using Microsoft.Extensions.Options;

namespace BlobSeek.UnitTests.Billing;

public sealed class PaymentAccessServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_AllowsAllFeatures_WhenSubscriptionRequirementIsOff()
    {
        var service = CreateService(requireActiveSubscription: false);

        var decision = await service.AuthorizeAsync(BillingFeature.Search, null, CancellationToken.None);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsAnonymousUser_WhenSubscriptionIsRequired()
    {
        var service = CreateService(requireActiveSubscription: true);

        var decision = await service.AuthorizeAsync(BillingFeature.Download, null, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal(BillingFailure.MissingUser, decision.Failure);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsUnpaidUser_WhenSubscriptionIsRequired()
    {
        var service = CreateService(requireActiveSubscription: true, paidUserIds: ["paid_user"]);

        var decision = await service.AuthorizeAsync(BillingFeature.Search, "free_user", CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal(BillingFailure.PaymentRequired, decision.Failure);
    }

    [Fact]
    public async Task AuthorizeAsync_AllowsConfiguredPaidUser_WhenSubscriptionIsRequired()
    {
        var service = CreateService(requireActiveSubscription: true, paidUserIds: ["paid_user"]);

        var decision = await service.AuthorizeAsync(BillingFeature.ManualIndexing, "paid_user", CancellationToken.None);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_AllowsEntitledUser_WhenSubscriptionIsRequired()
    {
        var service = CreateService(requireActiveSubscription: true, entitledUserIds: ["entitled_user"]);

        var decision = await service.AuthorizeAsync(BillingFeature.Download, "entitled_user", CancellationToken.None);

        Assert.True(decision.Allowed);
    }

    private static ConfiguredPaymentAccessService CreateService(
        bool requireActiveSubscription,
        IEnumerable<string>? paidUserIds = null,
        IEnumerable<string>? entitledUserIds = null)
    {
        return new ConfiguredPaymentAccessService(Options.Create(new BillingOptions
        {
            RequireActiveSubscription = requireActiveSubscription,
            PaidUserIds = paidUserIds?.ToHashSet(StringComparer.Ordinal) ?? []
        }), new InMemoryEntitlementStore(entitledUserIds ?? []));
    }

    private sealed class InMemoryEntitlementStore(IEnumerable<string> userIds) : IBillingEntitlementStore
    {
        private readonly HashSet<string> userIds = userIds.ToHashSet(StringComparer.Ordinal);

        public Task<bool> HasActiveEntitlementAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(userIds.Contains(userId));
    }
}
