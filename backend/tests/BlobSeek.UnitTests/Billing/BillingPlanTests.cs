using BlobSeek.Application.Billing;

namespace BlobSeek.UnitTests.Billing;

public sealed class BillingPlanTests
{
    [Theory]
    [InlineData(BillingPlan.Pro, "pro")]
    [InlineData(BillingPlan.Team, "team")]
    [InlineData(BillingPlan.Business, "business")]
    public void ToConfigurationKey_UsesStableExternalKeys(BillingPlan plan, string expected)
    {
        Assert.Equal(expected, plan.ToConfigurationKey());
    }
}
