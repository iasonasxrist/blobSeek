using System.IO.Compression;
using System.Text.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BlobSeek.Application.Billing;
using BlobSeek.Application.Indexing;
using BlobSeek.Application.Search;
using BlobSeek.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
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
var clerkIssuer = builder.Configuration["Clerk:Issuer"];
var authEnabled = !string.IsNullOrWhiteSpace(clerkIssuer);
if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Authority = clerkIssuer;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidIssuer = clerkIssuer,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });
}
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBlobSeekInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseRateLimiter();
if (authEnabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var api = app.MapGroup("/api").RequireRateLimiting("api");
if (authEnabled)
{
    api.RequireAuthorization();
}


api.MapPost("/billing/checkout", async Task<Results<Ok<CheckoutSessionResponse>, ProblemHttpResult>> (
    CheckoutRequest request,
    IBillingCheckoutService billing,
    ClaimsPrincipal user,
    CancellationToken cancellationToken) =>
{
    var result = await billing.CreateCheckoutSessionAsync(new CreateCheckoutSessionCommand(
        request.Plan,
        GetUserId(user) ?? "",
        request.IdempotencyKey), cancellationToken);

    if (result.Succeeded && result.Url is not null)
    {
        return TypedResults.Ok(new CheckoutSessionResponse(result.Url));
    }

    return ToProblem(result.Failure, result.Message);
})
.WithName("CreateCheckoutSession")
.WithOpenApi();

api.MapGet("/search", async Task<Results<Ok<SearchResponse>, ProblemHttpResult>> (
    ISearchIndex searchIndex,
    IPaymentAccessService paymentAccess,
    ClaimsPrincipal user,
    string? q,
    string? container,
    string? ext,
    string? sort,
    int? limit,
    int? offset,
    CancellationToken cancellationToken) =>
{
    var access = await paymentAccess.AuthorizeAsync(BillingFeature.Search, GetUserId(user), cancellationToken);
    if (!access.Allowed)
    {
        return ToProblem(access.Failure, access.Message);
    }

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

api.MapGet("/stats", async Task<Results<Ok<IndexingStats>, ProblemHttpResult>> (
    IIndexingStatsReader stats,
    IPaymentAccessService paymentAccess,
    ClaimsPrincipal user,
    CancellationToken cancellationToken) =>
{
    var access = await paymentAccess.AuthorizeAsync(BillingFeature.Search, GetUserId(user), cancellationToken);
    if (!access.Allowed)
    {
        return ToProblem(access.Failure, access.Message);
    }

    return TypedResults.Ok(await stats.GetStatsAsync(cancellationToken));
})
.WithName("Stats")
.WithOpenApi();

api.MapPost("/index/run", async Task<Results<Accepted<IndexingRunSummary>, ProblemHttpResult>> (
    IIndexingCoordinator coordinator,
    IPaymentAccessService paymentAccess,
    ClaimsPrincipal user,
    CancellationToken cancellationToken) =>
{
    var access = await paymentAccess.AuthorizeAsync(BillingFeature.ManualIndexing, GetUserId(user), cancellationToken);
    if (!access.Allowed)
    {
        return ToProblem(access.Failure, access.Message);
    }

    return TypedResults.Accepted($"/api/stats", await coordinator.RunOnceAsync(cancellationToken));
})
.WithName("RunIndexing")
.WithOpenApi();

api.MapGet("/download", async Task<Results<Ok<DownloadLinkResponse>, ProblemHttpResult>> (
    IBlobCatalog blobs,
    IPaymentAccessService paymentAccess,
    ClaimsPrincipal user,
    string storageAccount,
    string container,
    string path,
    CancellationToken cancellationToken) =>
{
    var access = await paymentAccess.AuthorizeAsync(BillingFeature.Download, GetUserId(user), cancellationToken);
    if (!access.Allowed)
    {
        return ToProblem(access.Failure, access.Message);
    }

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

static string? GetUserId(ClaimsPrincipal user) =>
    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

static ProblemHttpResult ToProblem(BillingFailure? failure, string? message) => failure switch
{
    BillingFailure.MissingUser => TypedResults.Problem(message ?? "Sign in required.", statusCode: StatusCodes.Status401Unauthorized),
    BillingFailure.MissingIdempotencyKey => TypedResults.Problem(message ?? "Idempotency key required.", statusCode: StatusCodes.Status400BadRequest),
    BillingFailure.PaymentRequired => TypedResults.Problem(message ?? "Payment is required.", statusCode: StatusCodes.Status402PaymentRequired),
    BillingFailure.MissingConfiguration => TypedResults.Problem(message ?? "Billing is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable),
    BillingFailure.ProviderFailure => TypedResults.Problem(message ?? "Payment provider failed.", statusCode: StatusCodes.Status502BadGateway),
    _ => TypedResults.Problem(message ?? "Billing failed.", statusCode: StatusCodes.Status400BadRequest)
};

public sealed record DownloadLinkResponse(Uri Url);

public partial class Program;
