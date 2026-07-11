using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using BlobSeek.Application.Indexing;
using BlobSeek.Application.Options;
using Microsoft.Extensions.Options;

namespace BlobSeek.Infrastructure.Azure;

internal sealed class AzureBlobCatalog : IBlobCatalog
{
    private readonly BlobSeekOptions _options;
    private readonly IReadOnlyDictionary<string, BlobServiceClient> _clients;

    public AzureBlobCatalog(IOptions<BlobSeekOptions> options)
    {
        _options = options.Value;
        _clients = _options.Storage.Accounts.ToDictionary(
            account => account.Name,
            CreateClient,
            StringComparer.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<BlobItemInfo> ListBlobsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var account in _options.Storage.Accounts)
        {
            var client = _clients[account.Name];
            var containers = account.Containers.Count > 0
                ? account.Containers
                : await ListContainerNamesAsync(client, cancellationToken);

            foreach (var containerName in containers)
            {
                var container = client.GetBlobContainerClient(containerName);
                await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    var properties = blob.Properties;
                    yield return new BlobItemInfo(
                        account.Name,
                        containerName,
                        blob.Name,
                        container.GetBlobClient(blob.Name).Uri,
                        properties.ETag?.ToString() ?? "",
                        properties.LastModified ?? DateTimeOffset.UnixEpoch,
                        properties.ContentLength ?? 0,
                        properties.ContentType ?? "");
                }
            }
        }
    }

    public async Task<Stream> OpenReadAsync(BlobItemInfo blob, CancellationToken cancellationToken)
    {
        var client = GetBlobClient(blob.StorageAccount, blob.ContainerName, blob.Name);
        var response = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public Task<Uri> CreateDownloadLinkAsync(
        string storageAccount,
        string containerName,
        string blobName,
        CancellationToken cancellationToken)
    {
        var client = GetBlobClient(storageAccount, containerName, blobName);
        if (!client.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "This credential cannot generate SAS URLs. Use a connection string/account key for download links, or place BlobSeek behind a proxy that can authorize downloads.");
        }

        var builder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.Storage.SasExpiryMinutes)
        };
        builder.SetPermissions(BlobSasPermissions.Read);
        return Task.FromResult(client.GenerateSasUri(builder));
    }

    private static BlobServiceClient CreateClient(StorageAccountOptions account)
    {
        if (!string.IsNullOrWhiteSpace(account.ConnectionString))
        {
            return new BlobServiceClient(account.ConnectionString);
        }

        if (account.ServiceUri is not null)
        {
            return new BlobServiceClient(account.ServiceUri, new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            $"Storage account '{account.Name}' must set either ConnectionString or ServiceUri.");
    }

    private static async Task<List<string>> ListContainerNamesAsync(
        BlobServiceClient client,
        CancellationToken cancellationToken)
    {
        var names = new List<string>();
        await foreach (var container in client.GetBlobContainersAsync(cancellationToken: cancellationToken))
        {
            names.Add(container.Name);
        }

        return names;
    }

    private BlobClient GetBlobClient(string storageAccount, string containerName, string blobName)
    {
        if (!_clients.TryGetValue(storageAccount, out var serviceClient))
        {
            throw new InvalidOperationException($"Storage account '{storageAccount}' is not configured.");
        }

        return serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
    }
}
