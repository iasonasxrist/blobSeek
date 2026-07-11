using BlobSeek.Application.Indexing;
using BlobSeek.Domain;
using BlobSeek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlobSeek.Infrastructure.Indexing;

internal sealed class IndexingStatsReader(BlobSeekDbContext db) : IIndexingStatsReader
{
    public async Task<IndexingStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        var documents = await db.BlobStates.CountAsync(x => x.Status == IndexingStatus.Indexed || x.Status == IndexingStatus.Skipped, cancellationToken);
        var containers = await db.BlobStates
            .Where(x => x.Status != IndexingStatus.Deleted)
            .Select(x => x.ContainerName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var statusRows = await db.BlobStates
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key.ToString(), Count = x.Count() })
            .ToListAsync(cancellationToken);

        var lastRun = await db.IndexingRuns
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastCompletedSync = await db.IndexingRuns
            .Where(x => x.Status == "Completed")
            .OrderByDescending(x => x.FinishedAt)
            .Select(x => x.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new IndexingStats(
            documents,
            lastCompletedSync,
            containers,
            statusRows.ToDictionary(x => x.Status, x => x.Count),
            lastRun is null ? null : new IndexingRunSummary(
                lastRun.Id,
                lastRun.Status,
                lastRun.ContainersScanned,
                lastRun.BlobsSeen,
                lastRun.BlobsIndexed,
                lastRun.BlobsSkipped,
                lastRun.BlobsFailed,
                lastRun.BlobsDeleted));
    }
}
