namespace BlobSeek.Application.Search;

public sealed record SearchDocument(
    string DocumentId,
    string StorageAccount,
    string Container,
    string BlobName,
    string FullPath,
    string Extension,
    string ContentType,
    long ContentLength,
    DateTimeOffset LastModified,
    string Text,
    string BlobUri);
