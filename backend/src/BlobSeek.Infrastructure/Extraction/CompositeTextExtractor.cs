using System.Security.Cryptography;
using System.Text;
using BlobSeek.Application.Indexing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace BlobSeek.Infrastructure.Extraction;

internal sealed class CompositeTextExtractor : ITextExtractor
{
    private static readonly HashSet<string> PlainTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "md", "csv", "tsv", "json", "log", "yml", "yaml", "xml", "html", "htm",
        "ini", "conf", "toml", "env", "sql", "py", "js", "ts", "tsx", "jsx", "sh",
        "ps1", "cs", "java", "go", "rb", "php", "css", "scss", "rs", "kt", "swift"
    };

    public string Version => "composite-v1";

    public bool CanExtract(string blobName, string contentType)
    {
        var ext = ExtensionOf(blobName);
        return PlainTextExtensions.Contains(ext)
            || ext is "pdf" or "docx"
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ExtractedText> ExtractAsync(
        string blobName,
        string contentType,
        Stream content,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var ext = ExtensionOf(blobName);

        string text = ext switch
        {
            "pdf" => ExtractPdf(bytes, maxCharacters),
            "docx" => ExtractDocx(bytes),
            _ when PlainTextExtensions.Contains(ext) || CanExtract(blobName, contentType) =>
                Encoding.UTF8.GetString(bytes),
            _ => ""
        };

        return new ExtractedText(Truncate(text, maxCharacters), hash);
    }

    public static string ExtensionOf(string blobName)
    {
        var name = blobName.Split('/').LastOrDefault() ?? blobName;
        var index = name.LastIndexOf('.');
        return index < 0 ? "" : name[(index + 1)..].ToLowerInvariant();
    }

    private static string ExtractPdf(byte[] bytes, int maxCharacters)
    {
        using var stream = new MemoryStream(bytes);
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
            if (builder.Length >= maxCharacters)
            {
                break;
            }
        }

        return builder.ToString();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return "";
        }

        var text = body.Descendants<Text>().Select(x => x.Text);
        var tables = body.Descendants<TableCell>().Select(cell => string.Join(" ", cell.Descendants<Text>().Select(x => x.Text)));
        return string.Join(Environment.NewLine, text.Concat(tables).Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string Truncate(string value, int maxCharacters) =>
        value.Length <= maxCharacters ? value : value[..maxCharacters];
}
