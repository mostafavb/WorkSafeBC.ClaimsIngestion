using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Infrastructure.Storage;

namespace WorkSafeBC.Claims.Tests.Integration;

public sealed class BlobStorageReaderIntegrationTests(LocalInfrastructureFixture infrastructure)
    : IClassFixture<LocalInfrastructureFixture>
{
    [Fact]
    public async Task GetPendingFilesAsync_Should_Read_Xml_And_Flat_Files_From_Azurite()
    {
        var containerName = $"claims-reader-{Guid.NewGuid():N}";
        var blobContainerClient = new BlobContainerClient(infrastructure.AzuriteConnectionString, containerName);
        await blobContainerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        await blobContainerClient.UploadBlobAsync("claims.xml", BinaryData.FromString("""
            <Claims>
              <Claim>
                <ClaimNumber>CLM-1001</ClaimNumber>
                <WorkerId>WRK-42</WorkerId>
                <InjuryDate>2026-06-13</InjuryDate>
                <ClaimAmount>1250.50</ClaimAmount>
                <EmployerNumber>EMP-1</EmployerNumber>
                <Currency>CAD</Currency>
              </Claim>
            </Claims>
            """)).ConfigureAwait(false);
        await blobContainerClient.UploadBlobAsync("claims.txt", BinaryData.FromString("CLM-2001|WRK-43|2026-06-12|100.25|EMP-2|CAD"))
            .ConfigureAwait(false);

        var reader = new BlobInboundClaimFileReader(blobContainerClient, NullLogger<BlobInboundClaimFileReader>.Instance);

        var files = await reader.GetPendingFilesAsync(CancellationToken.None).ConfigureAwait(false);

        files.Should().HaveCount(2);
        files.Should().Contain(x => x.FileName == "claims.xml" && x.Kind == ClaimFileKind.Xml);
        files.Should().Contain(x => x.FileName == "claims.txt" && x.Kind == ClaimFileKind.FlatFile);
        files.Single(x => x.FileName == "claims.xml").Content.Should().Contain("CLM-1001");
        files.Single(x => x.FileName == "claims.txt").Content.Should().Contain("CLM-2001");
    }
}
