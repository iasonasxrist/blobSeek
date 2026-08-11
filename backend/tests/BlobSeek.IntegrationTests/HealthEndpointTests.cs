using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BlobSeek.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsSuccess()
    {
        await using var factory = CreateFactory([]);

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task PublicConfig_ReturnsOnlyClerkPublishableKey()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Clerk:PublishableKey"] = "pk_test_public",
            ["Clerk:Issuer"] = "https://issuer.example",
            ["Stripe:SecretKey"] = "sk_test_private",
            ["ConnectionStrings:BlobSeek"] = "Host=private",
            ["BlobSeek:Storage:Accounts:0:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountKey=private"
        });

        var client = factory.CreateClient();
        var config = await client.GetFromJsonAsync<PublicConfigPayload>("/api/public-config");

        Assert.NotNull(config);
        Assert.Equal("pk_test_public", config.ClerkPublishableKey);
        var raw = await client.GetStringAsync("/api/public-config");
        Assert.DoesNotContain("sk_test_private", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=private", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("AccountKey=private", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("issuer.example", raw, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sk_test_not_public")]
    public async Task PublicConfig_DoesNotReturnMissingOrNonPublishableClerkKeys(string? configuredKey)
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Clerk:PublishableKey"] = configuredKey
        });

        var client = factory.CreateClient();
        var config = await client.GetFromJsonAsync<PublicConfigPayload>("/api/public-config");

        Assert.NotNull(config);
        Assert.Null(config.ClerkPublishableKey);
    }

    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> overrides)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    var values = new Dictionary<string, string?>
                    {
                        ["Infisical:Enabled"] = "false",
                        ["BlobSeek:Storage:Accounts:0:Name"] = "test",
                        ["BlobSeek:Storage:Accounts:0:ConnectionString"] = "UseDevelopmentStorage=true",
                        ["BlobSeek:Indexing:DataDirectory"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                        ["BlobSeek:Search:IndexDirectory"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
                    };

                    foreach (var pair in overrides)
                    {
                        values[pair.Key] = pair.Value;
                    }

                    configuration.AddInMemoryCollection(values);
                });
            });
    }

    private sealed record PublicConfigPayload(string? ClerkPublishableKey);

}
