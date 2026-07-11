namespace BlobSeek.Application.Indexing;

public interface IBlobCatalog
{
    IAsyncEnumerable<BlobItemInfo> ListBlobsAsync(CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(BlobItemInfo blob, CancellationToken cancellationToken);

    Task<Uri> CreateDownloadLinkAsync(
        string storageAccount,
        string containerName,
        string blobName,
        CancellationToken cancellationToken);
}
