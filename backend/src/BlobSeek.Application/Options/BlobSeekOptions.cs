namespace BlobSeek.Application.Options;

public sealed class BlobSeekOptions
{
    public const string SectionName = "BlobSeek";

    public StorageOptions Storage { get; set; } = new();

    public IndexingOptions Indexing { get; set; } = new();

    public SearchOptions Search { get; set; } = new();
}

public sealed class StorageOptions
{
    public List<StorageAccountOptions> Accounts { get; set; } = [];

    public int SasExpiryMinutes { get; set; } = 60;
}

public sealed class StorageAccountOptions
{
    public string Name { get; set; } = "default";

    public string? ConnectionString { get; set; }

    public Uri? ServiceUri { get; set; }

    public List<string> Containers { get; set; } = [];
}

public sealed class IndexingOptions
{
    public int BatchSize { get; set; } = 100;

    public int MaxConcurrency { get; set; } = 4;

    public int PollingIntervalSeconds { get; set; } = 300;

    public int MaxBlobMegabytes { get; set; } = 50;

    public int MaxTextCharacters { get; set; } = 50_000;

    public string ExtractorVersion { get; set; } = "v1";

    public string DataDirectory { get; set; } = "/data";

    public int LeaseMinutes { get; set; } = 30;
}

public sealed class SearchOptions
{
    public string IndexDirectory { get; set; } = "/data/lucene";
}
