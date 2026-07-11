namespace BlobSeek.Domain;

public sealed class IndexingRun
{
    public string Id { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public int ContainersScanned { get; set; }
    public int BlobsSeen { get; set; }
    public int BlobsIndexed { get; set; }
    public int BlobsSkipped { get; set; }
    public int BlobsFailed { get; set; }
    public int BlobsDeleted { get; set; }
    public string? FailureReason { get; set; }
}
