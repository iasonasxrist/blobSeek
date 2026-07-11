namespace BlobSeek.Application.Indexing;

public interface ITextExtractor
{
    string Version { get; }

    bool CanExtract(string blobName, string contentType);

    Task<ExtractedText> ExtractAsync(
        string blobName,
        string contentType,
        Stream content,
        int maxCharacters,
        CancellationToken cancellationToken);
}

public sealed record ExtractedText(string Text, string? ContentHash);
