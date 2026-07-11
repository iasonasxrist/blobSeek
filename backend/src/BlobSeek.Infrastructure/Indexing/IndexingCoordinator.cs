using BlobSeek.Application.Indexing;
using BlobSeek.Application.Options;
using BlobSeek.Application.Search;
using BlobSeek.Domain;
using BlobSeek.Infrastructure.Extraction;
using BlobSeek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Indexing;

internal sealed class IndexingCoordinator(
    IDbContextFactory<BlobSeekDbContext> dbFactory,
    IBlobCatalog blobCatalog,
    ITextExtractor extractor,
    ISearchIndex searchIndex,
    IOptions<BlobSeekOptions> options,
    ILogger<IndexingCoordinator> logger) : IIndexingCoordinator
{
    private const string LeaseScope = "global";
    private readonly BlobSeekOptions _options = options.Value;

    public async Task<IndexingRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var ownerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await TryAcquireLeaseAsync(db, ownerId, cancellationToken))
        {
            logger.LogInformation("Indexing skipped because another worker owns the {Scope} lease.", LeaseScope);
            return new IndexingRunSummary("", "SkippedLease", 0, 0, 0, 0, 0, 0);
        }

        var run = new IndexingRun { Id = Guid.NewGuid().ToString("N"), StartedAt = DateTimeOffset.UtcNow };
        db.IndexingRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var containersSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batch = new List<BlobItemInfo>(Math.Max(1, _options.Indexing.MaxConcurrency));

            await foreach (var blob in blobCatalog.ListBlobsAsync(cancellationToken))
            {
                containersSeen.Add($"{blob.StorageAccount}/{blob.ContainerName}");
                run.BlobsSeen++;
                batch.Add(blob);

                if (batch.Count >= Math.Max(1, _options.Indexing.MaxConcurrency))
                {
                    await ProcessBatchAsync(batch, run, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch, run, cancellationToken);
            }

            run.ContainersScanned = containersSeen.Count;
            await ReconcileDeletedAsync(run, cancellationToken);
            await searchIndex.CommitAsync(cancellationToken);
            run.Status = "Completed";
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Status = "Failed";
            run.FailureReason = ex.Message;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(ex, "Indexing run {RunId} failed.", run.Id);
            throw;
        }
        finally
        {
            await ReleaseLeaseAsync(ownerId, CancellationToken.None);
        }

        return ToSummary(run);
    }

    private async Task ProcessBatchAsync(
        IReadOnlyList<BlobItemInfo> batch,
        IndexingRun run,
        CancellationToken cancellationToken)
    {
        var outcomes = await Task.WhenAll(batch.Select(blob => IndexBlobAsync(blob, run.Id, cancellationToken)));

        foreach (var outcome in outcomes)
        {
            run.BlobsIndexed += outcome.Indexed;
            run.BlobsSkipped += outcome.Skipped;
            run.BlobsFailed += outcome.Failed;
        }

        if (run.BlobsSeen % Math.Max(1, _options.Indexing.BatchSize) <= batch.Count)
        {
            await searchIndex.CommitAsync(cancellationToken);
        }
    }

    private async Task<IndexOutcome> IndexBlobAsync(BlobItemInfo blob, string runId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var documentId = DocumentId(blob.StorageAccount, blob.ContainerName, blob.Name);
        var state = await db.BlobStates.FindAsync([documentId], cancellationToken);
        var needsIndexing = state is null
            || state.ETag != blob.ETag
            || state.ExtractorVersion != extractor.Version
            || state.Status is IndexingStatus.Failed or IndexingStatus.Deleted;

        if (state is null)
        {
            state = new BlobIndexState { DocumentId = documentId };
            db.BlobStates.Add(state);
        }

        state.StorageAccount = blob.StorageAccount;
        state.ContainerName = blob.ContainerName;
        state.BlobName = blob.Name;
        state.BlobUri = blob.Uri.ToString();
        state.ETag = blob.ETag;
        state.LastModified = blob.LastModified;
        state.ContentLength = blob.ContentLength;
        state.ContentType = blob.ContentType;
        state.LastAttemptAt = DateTimeOffset.UtcNow;
        state.LastSeenRunId = runId;
        state.ExtractorVersion = extractor.Version;

        if (!needsIndexing)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new IndexOutcome(0, 1, 0);
        }

        try
        {
            var maxBytes = _options.Indexing.MaxBlobMegabytes * 1024L * 1024L;
            var extension = CompositeTextExtractor.ExtensionOf(blob.Name);
            var text = "";
            string? hash = null;

            if (blob.ContentLength <= maxBytes && extractor.CanExtract(blob.Name, blob.ContentType))
            {
                await using var content = await blobCatalog.OpenReadAsync(blob, cancellationToken);
                var extracted = await extractor.ExtractAsync(
                    blob.Name,
                    blob.ContentType,
                    content,
                    _options.Indexing.MaxTextCharacters,
                    cancellationToken);
                text = extracted.Text;
                hash = extracted.ContentHash;
            }

            await searchIndex.UpsertAsync(
                new SearchDocument(
                    documentId,
                    blob.StorageAccount,
                    blob.ContainerName,
                    FileName(blob.Name),
                    blob.Name,
                    extension,
                    blob.ContentType,
                    blob.ContentLength,
                    blob.LastModified,
                    text,
                    blob.Uri.ToString()),
                cancellationToken);

            state.Status = blob.ContentLength > maxBytes ? IndexingStatus.Skipped : IndexingStatus.Indexed;
            state.IndexedAt = DateTimeOffset.UtcNow;
            state.ContentHash = hash;
            state.FailureReason = null;
            await db.SaveChangesAsync(cancellationToken);
            return new IndexOutcome(1, 0, 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            state.Status = IndexingStatus.Failed;
            state.FailureReason = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Failed to index {Container}/{Blob}.", blob.ContainerName, blob.Name);
            return new IndexOutcome(0, 0, 1);
        }
    }

    private async Task ReconcileDeletedAsync(IndexingRun run, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await db.BlobStates
            .Where(x => x.Status != IndexingStatus.Deleted && x.LastSeenRunId != run.Id)
            .ToListAsync(cancellationToken);

        foreach (var state in deleted)
        {
            state.Status = IndexingStatus.Deleted;
            state.LastAttemptAt = DateTimeOffset.UtcNow;
            await searchIndex.DeleteAsync(state.DocumentId, cancellationToken);
            run.BlobsDeleted++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TryAcquireLeaseAsync(
        BlobSeekDbContext db,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lease = await db.IndexerLeases.FindAsync([LeaseScope], cancellationToken);
        if (lease is not null && lease.ExpiresAt > now && lease.OwnerId != ownerId)
        {
            return false;
        }

        if (lease is null)
        {
            lease = new IndexerLease { Scope = LeaseScope };
            db.IndexerLeases.Add(lease);
        }

        lease.OwnerId = ownerId;
        lease.AcquiredAt = now;
        lease.ExpiresAt = now.AddMinutes(_options.Indexing.LeaseMinutes);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ReleaseLeaseAsync(string ownerId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lease = await db.IndexerLeases.FindAsync([LeaseScope], cancellationToken);
        if (lease is not null && lease.OwnerId == ownerId)
        {
            db.IndexerLeases.Remove(lease);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static IndexingRunSummary ToSummary(IndexingRun run) =>
        new(run.Id, run.Status, run.ContainersScanned, run.BlobsSeen, run.BlobsIndexed, run.BlobsSkipped, run.BlobsFailed, run.BlobsDeleted);

    private static string DocumentId(string storageAccount, string container, string blobName)
    {
        var raw = $"{storageAccount}/{container}/{blobName}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private static string FileName(string path) => path.Split('/').LastOrDefault() ?? path;

    private sealed record IndexOutcome(int Indexed, int Skipped, int Failed);
}
