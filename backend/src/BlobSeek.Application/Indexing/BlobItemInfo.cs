namespace BlobSeek.Application.Indexing;

public sealed record BlobItemInfo(
    string StorageAccount,
    string ContainerName,
    string Name,
    Uri Uri,
    string ETag,
    DateTimeOffset LastModified,
    long ContentLength,
    string ContentType);
