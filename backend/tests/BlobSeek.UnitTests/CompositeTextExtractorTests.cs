using System.Text;
using BlobSeek.Infrastructure.Extraction;

namespace BlobSeek.UnitTests;

public sealed class CompositeTextExtractorTests
{
    [Theory]
    [InlineData("notes/readme.md", "md")]
    [InlineData("archive/report.PDF", "pdf")]
    [InlineData("no-extension", "")]
    public void ExtensionOf_ReturnsNormalizedExtension(string name, string expected)
    {
        Assert.Equal(expected, CompositeTextExtractor.ExtensionOf(name));
    }

    [Fact]
    public async Task ExtractAsync_TruncatesPlainTextAndReturnsHash()
    {
        var extractor = new CompositeTextExtractor();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("alpha beta gamma"));

        var result = await extractor.ExtractAsync("sample.txt", "text/plain", stream, 10, CancellationToken.None);

        Assert.Equal("alpha beta", result.Text);
        Assert.False(string.IsNullOrWhiteSpace(result.ContentHash));
    }
}
