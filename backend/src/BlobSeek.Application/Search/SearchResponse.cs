namespace BlobSeek.Application.Search;

public sealed record SearchResponse(
    IReadOnlyList<SearchHit> Hits,
    long Total,
    SearchFacets Facets,
    long ProcessingMilliseconds);

public sealed record SearchHit(
    string DocumentId,
    string Name,
    string NameHtml,
    string Path,
    string PathHtml,
    string StorageAccount,
    string Container,
    string Extension,
    string ContentType,
    long Size,
    DateTimeOffset LastModified,
    string ExcerptHtml);

public sealed record SearchFacets(
    IReadOnlyDictionary<string, int> Containers,
    IReadOnlyDictionary<string, int> Extensions);
