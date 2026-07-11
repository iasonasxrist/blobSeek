namespace BlobSeek.Application.Search;

public interface ISearchIndex
{
    Task UpsertAsync(SearchDocument document, CancellationToken cancellationToken);

    Task DeleteAsync(string documentId, CancellationToken cancellationToken);

    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}
