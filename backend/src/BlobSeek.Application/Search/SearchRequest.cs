namespace BlobSeek.Application.Search;

public sealed record SearchRequest(
    string Query,
    string? Container,
    string? Extension,
    string? Sort,
    int Limit,
    int Offset);
