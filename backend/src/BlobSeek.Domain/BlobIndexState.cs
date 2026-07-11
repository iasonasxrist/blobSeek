namespace BlobSeek.Domain;

public sealed class BlobIndexState
{
    public string DocumentId { get; set; } = "";
    public string StorageAccount { get; set; } = "";
    public string ContainerName { get; set; } = "";
    public string BlobName { get; set; } = "";
    public string BlobUri { get; set; } = "";
    public string ETag { get; set; } = "";
    public DateTimeOffset LastModified { get; set; }
    public long ContentLength { get; set; }
    public string ContentType { get; set; } = "";
    public DateTimeOffset? IndexedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public IndexingStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public string ExtractorVersion { get; set; } = "";
    public string? ContentHash { get; set; }
    public string? LastSeenRunId { get; set; }
}
