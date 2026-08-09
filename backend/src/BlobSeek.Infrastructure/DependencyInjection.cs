using BlobSeek.Application.Indexing;
using BlobSeek.Application.Options;
using BlobSeek.Application.Search;
using BlobSeek.Infrastructure.Azure;
using BlobSeek.Infrastructure.Extraction;
using BlobSeek.Infrastructure.Indexing;
using BlobSeek.Infrastructure.Persistence;
using BlobSeek.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlobSeek.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBlobSeekInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BlobSeekOptions>()
            .Bind(configuration.GetSection(BlobSeekOptions.SectionName))
            .Validate(options => options.Storage.Accounts.Count > 0, "At least one storage account is required.")
            .ValidateOnStart();

        var options = configuration.GetSection(BlobSeekOptions.SectionName).Get<BlobSeekOptions>() ?? new BlobSeekOptions();
        var dataDirectory = options.Indexing.DataDirectory;
        Directory.CreateDirectory(dataDirectory);

        var connectionString = configuration.GetConnectionString("BlobSeek")
            ?? "Host=postgres;Port=5432;Database=blobseek;Username=blobseek;Password=blobseek";

        services.AddDbContextFactory<BlobSeekDbContext>(db => db.UseNpgsql(connectionString));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<BlobSeekDbContext>>().CreateDbContext());

        services.AddSingleton<IBlobCatalog, AzureBlobCatalog>();
        services.AddSingleton<ITextExtractor, CompositeTextExtractor>();
        services.AddSingleton<ISearchIndex, LuceneSearchIndex>();
        services.AddScoped<IIndexingCoordinator, IndexingCoordinator>();
        services.AddScoped<IIndexingStatsReader, IndexingStatsReader>();
        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
