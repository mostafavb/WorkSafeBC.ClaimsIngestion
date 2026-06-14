using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace WorkSafeBC.Claims.Tests.Integration;

public sealed class LocalInfrastructureFixture : IAsyncLifetime
{
    private const ushort AzuriteBlobPort = 10000;
    private const ushort RabbitMqPort = 5672;

    private readonly IContainer _azuriteContainer = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithCommand("azurite", "--blobHost", "0.0.0.0", "--skipApiVersionCheck")
        .WithPortBinding(AzuriteBlobPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(AzuriteBlobPort))
        .Build();

    private readonly IContainer _rabbitMqContainer = new ContainerBuilder("rabbitmq:3.13-management")
        .WithPortBinding(RabbitMqPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RabbitMqPort))
        .Build();

    public string AzuriteConnectionString =>
        $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AzuriteAccountKey};BlobEndpoint=http://{_azuriteContainer.Hostname}:{_azuriteContainer.GetMappedPublicPort(AzuriteBlobPort)}/devstoreaccount1;";

    public string RabbitMqHostName => _rabbitMqContainer.Hostname;

    public int RabbitMqPublicPort => _rabbitMqContainer.GetMappedPublicPort(RabbitMqPort);

    public async Task InitializeAsync()
    {
        await _azuriteContainer.StartAsync().ConfigureAwait(false);
        await _rabbitMqContainer.StartAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync().ConfigureAwait(false);
        await _azuriteContainer.DisposeAsync().ConfigureAwait(false);
    }

    private const string AzuriteAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
}
