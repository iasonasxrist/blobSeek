using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BlobSeek.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BlobSeek:Storage:Accounts:0:Name"] = "test",
                        ["BlobSeek:Storage:Accounts:0:ConnectionString"] = "UseDevelopmentStorage=true",
                        ["BlobSeek:Indexing:DataDirectory"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                        ["BlobSeek:Search:IndexDirectory"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
                    });
                });
            });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
