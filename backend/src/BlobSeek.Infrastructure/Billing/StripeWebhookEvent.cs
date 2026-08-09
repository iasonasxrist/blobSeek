namespace BlobSeek.Infrastructure.Billing;

public sealed class StripeWebhookEvent
{
    public string EventId { get; set; } = "";

    public string Type { get; set; } = "";

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
