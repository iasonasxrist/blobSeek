namespace BlobSeek.Domain;

public sealed class IndexerLease
{
    public string Scope { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public DateTimeOffset AcquiredAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
