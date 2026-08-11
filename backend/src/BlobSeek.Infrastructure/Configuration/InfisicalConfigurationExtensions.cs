using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BlobSeek.Infrastructure.Configuration;

public static class InfisicalConfigurationExtensions
{
    public static IConfigurationBuilder AddInfisicalWhenConfigured(
        this IConfigurationBuilder builder,
        IHostEnvironment environment)
    {
        var bootstrap = builder.Build();
        var options = InfisicalOptions.FromConfiguration(bootstrap, environment);

        if (!options.Enabled)
        {
            return builder;
        }

        return builder.Add(new InfisicalConfigurationSource(options));
    }
}

internal sealed class InfisicalConfigurationSource(InfisicalOptions options) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new InfisicalConfigurationProvider(options);
}

internal sealed class InfisicalConfigurationProvider(InfisicalOptions options) : ConfigurationProvider
{
    public override void Load()
    {
        if (string.IsNullOrWhiteSpace(options.ProjectId) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("Infisical is enabled, but ProjectId, ClientId, or ClientSecret is missing.");
        }

        using var http = new HttpClient { BaseAddress = options.ApiUrl };
        var token = Login(http);
        Data = ListSecrets(http, token);
    }

    private string Login(HttpClient http)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new
            {
                clientId = options.ClientId,
                clientSecret = options.ClientSecret,
                organizationSlug = string.IsNullOrWhiteSpace(options.OrganizationSlug) ? null : options.OrganizationSlug
            }),
            Encoding.UTF8,
            "application/json");

        using var response = http.PostAsync("/api/v1/auth/universal-auth/login", content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStream();
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Infisical login did not return an access token.");
    }

    private IDictionary<string, string?> ListSecrets(HttpClient http, string token)
    {
        var query = new Dictionary<string, string?>
        {
            ["projectId"] = options.ProjectId,
            ["environment"] = options.Environment,
            ["secretPath"] = options.SecretPath,
            ["viewSecretValue"] = "true",
            ["expandSecretReferences"] = "true",
            ["recursive"] = options.Recursive ? "true" : "false"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryString.Build("/api/v4/secrets", query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = http.Send(request);
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStream();
        using var document = JsonDocument.Parse(stream);
        var secrets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        AddSecrets(document.RootElement.GetProperty("secrets"), secrets);

        if (document.RootElement.TryGetProperty("imports", out var imports))
        {
            foreach (var import in imports.EnumerateArray())
            {
                if (import.TryGetProperty("secrets", out var importedSecrets))
                {
                    AddSecrets(importedSecrets, secrets);
                }
            }
        }

        return secrets;
    }

    private static void AddSecrets(JsonElement items, IDictionary<string, string?> secrets)
    {
        foreach (var item in items.EnumerateArray())
        {
            var key = item.GetProperty("secretKey").GetString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            secrets[key.Replace("__", ":", StringComparison.Ordinal)] =
                item.TryGetProperty("secretValue", out var value) ? value.GetString() : null;
        }
    }
}

internal sealed record InfisicalOptions(
    bool Enabled,
    Uri ApiUrl,
    string ProjectId,
    string Environment,
    string SecretPath,
    bool Recursive,
    string ClientId,
    string ClientSecret,
    string? OrganizationSlug)
{
    public static InfisicalOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = configuration.GetValue("Infisical:Enabled", false);
        var clientId = configuration["Infisical:ClientId"] ?? configuration["INFISICAL_UNIVERSAL_AUTH_CLIENT_ID"] ?? "";
        var clientSecret = configuration["Infisical:ClientSecret"] ?? configuration["INFISICAL_UNIVERSAL_AUTH_CLIENT_SECRET"] ?? "";
        var projectId = configuration["Infisical:ProjectId"] ?? configuration["INFISICAL_PROJECT_ID"] ?? "";
        var apiUrl = configuration["Infisical:ApiUrl"] ?? configuration["INFISICAL_API_URL"] ?? "https://us.infisical.com";
        var infisicalEnvironment = configuration["Infisical:Environment"]
            ?? configuration["INFISICAL_ENVIRONMENT"]
            ?? (environment.IsDevelopment() ? "dev" : "prod");

        return new InfisicalOptions(
            enabled,
            new Uri(apiUrl, UriKind.Absolute),
            projectId,
            infisicalEnvironment,
            configuration["Infisical:SecretPath"] ?? configuration["INFISICAL_SECRET_PATH"] ?? "/",
            configuration.GetValue("Infisical:Recursive", true),
            clientId,
            clientSecret,
            configuration["Infisical:OrganizationSlug"] ?? configuration["INFISICAL_ORGANIZATION_SLUG"]);
    }
}

internal static class QueryString
{
    public static string Build(string path, IReadOnlyDictionary<string, string?> query)
    {
        var builder = new StringBuilder(path);
        var separator = '?';

        foreach (var (key, value) in query)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder.Append(separator);
            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return builder.ToString();
    }
}
