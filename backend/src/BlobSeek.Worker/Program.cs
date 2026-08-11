using BlobSeek.Application.Indexing;
using BlobSeek.Application.Options;
using BlobSeek.Infrastructure;
using BlobSeek.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddInfisicalWhenConfigured(builder.Environment);

builder.Services.AddSerilog((services, configuration) =>
    configuration.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());
builder.Services.AddBlobSeekInfrastructure(builder.Configuration);
builder.Services.AddHostedService<IndexingWorker>();

await builder.Build().RunAsync();

internal sealed class IndexingWorker(
    IServiceProvider services,
    IOptions<BlobSeekOptions> options,
    ILogger<IndexingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(30, options.Value.Indexing.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<IIndexingCoordinator>();
                var summary = await coordinator.RunOnceAsync(stoppingToken);
                logger.LogInformation(
                    "Indexing run {RunId} finished with {Status}: seen={Seen}, indexed={Indexed}, skipped={Skipped}, failed={Failed}, deleted={Deleted}.",
                    summary.RunId,
                    summary.Status,
                    summary.BlobsSeen,
                    summary.BlobsIndexed,
                    summary.BlobsSkipped,
                    summary.BlobsFailed,
                    summary.BlobsDeleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Indexing worker iteration failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
