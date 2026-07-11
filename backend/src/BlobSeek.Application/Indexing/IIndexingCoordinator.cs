namespace BlobSeek.Application.Indexing;

public interface IIndexingCoordinator
{
    Task<IndexingRunSummary> RunOnceAsync(CancellationToken cancellationToken);
}

public sealed record IndexingRunSummary(
    string RunId,
    string Status,
    int ContainersScanned,
    int BlobsSeen,
    int BlobsIndexed,
    int BlobsSkipped,
    int BlobsFailed,
    int BlobsDeleted);
