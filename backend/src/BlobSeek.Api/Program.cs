using System.IO.Compression;
using System.Threading.RateLimiting;
using BlobSeek.Application.Indexing;
using BlobSeek.Application.Search;
using BlobSeek.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 20;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBlobSeekInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseRateLimiter();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var api = app.MapGroup("/api").RequireRateLimiting("api");

api.MapGet("/search", async Task<Results<Ok<SearchResponse>, ProblemHttpResult>> (
    ISearchIndex searchIndex,
    string? q,
    string? container,
    string? ext,
    string? sort,
    int? limit,
    int? offset,
    CancellationToken cancellationToken) =>
{
    if (sort is not null && sort is not ("last_modified:desc" or "last_modified:asc" or "size:desc" or "size:asc"))
    {
        return TypedResults.Problem("Unsupported sort expression.", statusCode: StatusCodes.Status400BadRequest);
    }

    var request = new SearchRequest(
        q ?? "",
        string.IsNullOrWhiteSpace(container) ? null : container,
        string.IsNullOrWhiteSpace(ext) ? null : ext.TrimStart('.'),
        sort,
        Math.Clamp(limit ?? 20, 1, 100),
        Math.Max(0, offset ?? 0));

    return TypedResults.Ok(await searchIndex.SearchAsync(request, cancellationToken));
})
.WithName("Search")
.WithOpenApi();

api.MapGet("/stats", async (IIndexingStatsReader stats, CancellationToken cancellationToken) =>
    TypedResults.Ok(await stats.GetStatsAsync(cancellationToken)))
    .WithName("Stats")
    .WithOpenApi();

api.MapPost("/index/run", async (IIndexingCoordinator coordinator, CancellationToken cancellationToken) =>
    TypedResults.Accepted($"/api/stats", await coordinator.RunOnceAsync(cancellationToken)))
    .WithName("RunIndexing")
    .WithOpenApi();

api.MapGet("/download", async Task<Results<Ok<DownloadLinkResponse>, ProblemHttpResult>> (
    IBlobCatalog blobs,
    string storageAccount,
    string container,
    string path,
    CancellationToken cancellationToken) =>
{
    try
    {
        var uri = await blobs.CreateDownloadLinkAsync(storageAccount, container, path, cancellationToken);
        return TypedResults.Ok(new DownloadLinkResponse(uri));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("Download")
.WithOpenApi();

app.MapHealthChecks("/health");
app.MapFallback(() => Results.Redirect("/swagger"));

app.Run();

public sealed record DownloadLinkResponse(Uri Url);

public partial class Program;
