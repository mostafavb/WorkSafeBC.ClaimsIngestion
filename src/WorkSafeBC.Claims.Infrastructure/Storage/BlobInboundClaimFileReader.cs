using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;

namespace WorkSafeBC.Claims.Infrastructure.Storage;

public sealed class BlobInboundClaimFileReader(
    BlobContainerClient containerClient,
    ILogger<BlobInboundClaimFileReader> logger)
    : IInboundClaimFileReader
{
    public async Task<IReadOnlyCollection<InboundClaimFile>> GetPendingFilesAsync(CancellationToken cancellationToken)
    {
        var files = new List<InboundClaimFile>();

        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            var blobClient = containerClient.GetBlobClient(blobItem.Name);
            var download = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            var content = download.Value.Content.ToString();
            var kind = Path.GetExtension(blobItem.Name).Equals(".xml", StringComparison.OrdinalIgnoreCase)
                ? ClaimFileKind.Xml
                : ClaimFileKind.FlatFile;

            files.Add(new InboundClaimFile(
                blobItem.Name,
                content,
                kind,
                blobItem.Properties.LastModified ?? DateTimeOffset.UtcNow));
        }

        if (files.Count > 0)
        {
            logger.LogInformation("Discovered {FileCount} claim files in storage.", files.Count);
        }

        return files;
    }
}
