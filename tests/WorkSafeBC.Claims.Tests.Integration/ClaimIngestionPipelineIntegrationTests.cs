using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using WorkSafeBC.Claims.Application;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Application.Contracts;
using WorkSafeBC.Claims.Infrastructure;

namespace WorkSafeBC.Claims.Tests.Integration;

public sealed class ClaimIngestionPipelineIntegrationTests(LocalInfrastructureFixture infrastructure)
    : IClassFixture<LocalInfrastructureFixture>
{
    [Fact]
    public async Task Pipeline_Should_Read_From_Azurite_And_Publish_To_RabbitMq()
    {
        var containerName = $"claims-pipeline-{Guid.NewGuid():N}";
        var exchangeName = $"claims.ingestion.{Guid.NewGuid():N}";
        var routingKey = "claims.ingested";
        var blobContainerClient = new BlobContainerClient(infrastructure.AzuriteConnectionString, containerName);
        await blobContainerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        await blobContainerClient.UploadBlobAsync("batch.xml", BinaryData.FromString("""
            <Claims>
              <Claim>
                <ClaimNumber>CLM-3001</ClaimNumber>
                <WorkerId>WRK-99</WorkerId>
                <InjuryDate>2026-06-13</InjuryDate>
                <ClaimAmount>2500.75</ClaimAmount>
                <EmployerNumber>EMP-9</EmployerNumber>
                <Currency>CAD</Currency>
              </Claim>
            </Claims>
            """)).ConfigureAwait(false);

        using var rabbitMqConnection = CreateConnectionFactory().CreateConnection();
        using var rabbitMqChannel = rabbitMqConnection.CreateModel();
        rabbitMqChannel.ExchangeDeclare(exchangeName, ExchangeType.Topic, durable: true);
        var queueName = rabbitMqChannel.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true).QueueName;
        rabbitMqChannel.QueueBind(queueName, exchangeName, routingKey);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClaimsStorage:ConnectionString"] = infrastructure.AzuriteConnectionString,
                ["ClaimsStorage:ContainerName"] = containerName,
                ["RabbitMq:HostName"] = infrastructure.RabbitMqHostName,
                ["RabbitMq:Port"] = infrastructure.RabbitMqPublicPort.ToString(),
                ["RabbitMq:UserName"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["RabbitMq:ExchangeName"] = exchangeName,
                ["RabbitMq:RoutingKey"] = routingKey,
                ["KeyVault:VaultUri"] = string.Empty
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        services.AddApplication();
        services.AddInfrastructure(configuration);

        await using var serviceProvider = services.BuildServiceProvider();
        var reader = serviceProvider.GetRequiredService<IInboundClaimFileReader>();
        var handler = serviceProvider.GetRequiredService<ICommandHandler<ProcessClaimFileCommand, ProcessClaimFileResult>>();

        var pendingFiles = await reader.GetPendingFilesAsync(CancellationToken.None).ConfigureAwait(false);
        var result = await handler.HandleAsync(new ProcessClaimFileCommand(pendingFiles.Single()), CancellationToken.None).ConfigureAwait(false);

        result.WasSkipped.Should().BeFalse();
        result.ParsedClaims.Should().Be(1);
        result.PublishedEvents.Should().Be(1);

        var deliveredBody = await ReceiveMessageAsync(rabbitMqChannel, queueName).ConfigureAwait(false);
        var publishedEvent = JsonSerializer.Deserialize<ClaimIngestionEvent>(deliveredBody);

        publishedEvent.Should().NotBeNull();
        publishedEvent!.ClaimNumber.Should().Be("CLM-3001");
        publishedEvent.WorkerId.Should().Be("WRK-99");
        publishedEvent.SourceFileName.Should().Be("batch.xml");
        publishedEvent.ClaimAmount.Should().Be(2500.75m);
    }

    private ConnectionFactory CreateConnectionFactory() =>
        new()
        {
            HostName = infrastructure.RabbitMqHostName,
            Port = infrastructure.RabbitMqPublicPort,
            UserName = "guest",
            Password = "guest",
            DispatchConsumersAsync = true
        };

    private static async Task<string> ReceiveMessageAsync(IModel channel, string queueName)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var message = channel.BasicGet(queueName, autoAck: true);
            if (message is not null)
            {
                return Encoding.UTF8.GetString(message.Body.ToArray());
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new InvalidOperationException("No message was published to RabbitMQ.");
    }
}
