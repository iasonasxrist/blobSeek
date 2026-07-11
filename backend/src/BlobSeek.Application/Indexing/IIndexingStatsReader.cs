namespace BlobSeek.Application.Indexing;

public interface IIndexingStatsReader
{
    Task<IndexingStats> GetStatsAsync(CancellationToken cancellationToken);
}

public sealed record IndexingStats(
    long Documents,
    DateTimeOffset? LastCompletedSync,
    IReadOnlyList<string> Containers,
    IReadOnlyDictionary<string, int> StatusCounts,
    IndexingRunSummary? LastRun);
