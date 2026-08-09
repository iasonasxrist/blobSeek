namespace BlobSeek.Application.Billing;

public static class BillingPlanExtensions
{
    public static string ToConfigurationKey(this BillingPlan plan) => plan switch
    {
        BillingPlan.Pro => "pro",
        BillingPlan.Team => "team",
        BillingPlan.Business => "business",
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unsupported billing plan.")
    };
}
