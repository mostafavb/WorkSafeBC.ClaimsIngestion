using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Contracts;

namespace WorkSafeBC.Claims.Infrastructure.Messaging;

public sealed class RabbitMqClaimMessagePublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqClaimMessagePublisher> logger)
    : IClaimMessagePublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public Task PublishAsync(ClaimIngestionEvent integrationEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true);

        var payload = JsonSerializer.Serialize(integrationEvent);
        var body = Encoding.UTF8.GetBytes(payload);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            basicProperties: properties,
            body: body);

        logger.LogInformation(
            "Published claim event {ClaimNumber} from file {FileName}.",
            integrationEvent.ClaimNumber,
            integrationEvent.SourceFileName);

        return Task.CompletedTask;
    }
}
