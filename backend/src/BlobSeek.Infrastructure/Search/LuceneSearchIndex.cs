using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using BlobSeek.Application.Options;
using BlobSeek.Application.Search;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Search;

internal sealed partial class LuceneSearchIndex : ISearchIndex, IDisposable
{
    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private readonly StandardAnalyzer _analyzer;
    private readonly FSDirectory _directory;
    private readonly IndexWriter _writer;
    private readonly object _gate = new();

    public LuceneSearchIndex(IOptions<BlobSeekOptions> options)
    {
        var indexDirectory = options.Value.Search.IndexDirectory;
        System.IO.Directory.CreateDirectory(indexDirectory);
        _directory = FSDirectory.Open(new DirectoryInfo(indexDirectory));
        _analyzer = new StandardAnalyzer(Version);
        var config = new IndexWriterConfig(Version, _analyzer);
        _writer = new IndexWriter(_directory, config);
    }

    public Task UpsertAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var luceneDocument = new Document
        {
            new StringField("id", document.DocumentId, Field.Store.YES),
            new StringField("storageAccount", document.StorageAccount, Field.Store.YES),
            new StringField("container", document.Container, Field.Store.YES),
            new StringField("extension", document.Extension, Field.Store.YES),
            new TextField("name", FileName(document.FullPath), Field.Store.YES),
            new TextField("path", document.FullPath, Field.Store.YES),
            new TextField("text", document.Text, Field.Store.YES),
            new StoredField("contentType", document.ContentType),
            new StoredField("size", document.ContentLength),
            new Int64Field("lastModified", document.LastModified.ToUnixTimeSeconds(), Field.Store.YES),
            new NumericDocValuesField("lastModifiedSort", document.LastModified.ToUnixTimeSeconds()),
            new Int64Field("sizeValue", document.ContentLength, Field.Store.NO),
            new NumericDocValuesField("sizeSort", document.ContentLength),
            new StoredField("blobUri", document.BlobUri)
        };

        lock (_gate)
        {
            _writer.UpdateDocument(new Term("id", document.DocumentId), luceneDocument);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _writer.DeleteDocuments(new Term("id", documentId));
        }

        return Task.CompletedTask;
    }

    public Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        lock (_gate)
        {
            _writer.Commit();
        }

        using var reader = DirectoryReader.Open(_directory);
        var searcher = new IndexSearcher(reader);
        var query = BuildQuery(request);
        var requested = Math.Clamp(request.Limit, 1, 100);
        var offset = Math.Max(0, request.Offset);
        var sort = BuildSort(request.Sort);
        var top = sort is null
            ? searcher.Search(query, offset + requested)
            : searcher.Search(query, offset + requested, sort);

        var docs = top.ScoreDocs.Skip(offset).Take(requested).Select(hit => searcher.Doc(hit.Doc)).ToList();
        var terms = QueryTerms(request.Query);
        var hits = docs.Select(document => ToHit(document, terms)).ToList();
        var facets = BuildFacets(searcher, query);
        stopwatch.Stop();

        return Task.FromResult(new SearchResponse(hits, top.TotalHits, facets, stopwatch.ElapsedMilliseconds));
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _writer.Commit();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _writer.Dispose();
        _directory.Dispose();
        _analyzer.Dispose();
    }

    private Query BuildQuery(SearchRequest request)
    {
        Query textQuery;
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            textQuery = new MatchAllDocsQuery();
        }
        else
        {
            var parser = new MultiFieldQueryParser(Version, ["name", "path", "text"], _analyzer);
            textQuery = parser.Parse(QueryParserBase.Escape(request.Query.Trim()));
        }

        var filterQuery = new BooleanQuery { { textQuery, Occur.MUST } };
        if (!string.IsNullOrWhiteSpace(request.Container))
        {
            filterQuery.Add(new TermQuery(new Term("container", request.Container)), Occur.MUST);
        }

        if (!string.IsNullOrWhiteSpace(request.Extension))
        {
            filterQuery.Add(new TermQuery(new Term("extension", request.Extension.TrimStart('.'))), Occur.MUST);
        }

        return filterQuery;
    }

    private static Sort? BuildSort(string? sort) =>
        sort switch
        {
            "last_modified:desc" => new Sort(new SortField("lastModifiedSort", SortFieldType.INT64, true)),
            "last_modified:asc" => new Sort(new SortField("lastModifiedSort", SortFieldType.INT64, false)),
            "size:desc" => new Sort(new SortField("sizeSort", SortFieldType.INT64, true)),
            "size:asc" => new Sort(new SortField("sizeSort", SortFieldType.INT64, false)),
            _ => null
        };

    private static SearchHit ToHit(Document document, IReadOnlyList<string> terms)
    {
        var path = document.Get("path") ?? "";
        var name = document.Get("name") ?? FileName(path);
        var text = document.Get("text") ?? "";
        var lastModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(document.Get("lastModified") ?? "0"));
        return new SearchHit(
            document.Get("id") ?? "",
            name,
            Highlight(name, terms),
            path,
            Highlight(path, terms),
            document.Get("storageAccount") ?? "",
            document.Get("container") ?? "",
            document.Get("extension") ?? "",
            document.Get("contentType") ?? "",
            long.Parse(document.Get("size") ?? "0"),
            lastModified,
            Excerpt(text, terms));
    }

    private static SearchFacets BuildFacets(IndexSearcher searcher, Query query)
    {
        var top = searcher.Search(query, 10_000);
        Dictionary<string, int> containers = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> extensions = new(StringComparer.OrdinalIgnoreCase);

        foreach (var scoreDoc in top.ScoreDocs)
        {
            var document = searcher.Doc(scoreDoc.Doc);
            AddFacet(containers, document.Get("container"));
            AddFacet(extensions, document.Get("extension"));
        }

        return new SearchFacets(
            containers.OrderByDescending(x => x.Value).ThenBy(x => x.Key).ToDictionary(),
            extensions.Where(x => !string.IsNullOrWhiteSpace(x.Key)).OrderByDescending(x => x.Value).ThenBy(x => x.Key).ToDictionary());
    }

    private static void AddFacet(IDictionary<string, int> values, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        values[key] = values.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static string Highlight(string value, IReadOnlyList<string> terms)
    {
        var encoded = WebUtility.HtmlEncode(value);
        foreach (var term in terms)
        {
            encoded = Regex.Replace(
                encoded,
                Regex.Escape(WebUtility.HtmlEncode(term)),
                match => $"<mark>{match.Value}</mark>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return encoded;
    }

    private static string Excerpt(string text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var index = terms.Count == 0
            ? 0
            : terms.Select(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase)).Where(x => x >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, index - 120);
        var length = Math.Min(text.Length - start, 280);
        var excerpt = text.Substring(start, length).ReplaceLineEndings(" ");
        return Highlight((start > 0 ? "..." : "") + excerpt + (start + length < text.Length ? "..." : ""), terms);
    }

    private static IReadOnlyList<string> QueryTerms(string query) =>
        WordRegex().Matches(query)
            .Select(match => match.Value)
            .Where(term => term.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

    private static string FileName(string path) => path.Split('/').LastOrDefault() ?? path;

    [GeneratedRegex("[A-Za-z0-9_./-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
