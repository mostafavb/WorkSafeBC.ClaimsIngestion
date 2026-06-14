using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Infrastructure.Messaging;
using WorkSafeBC.Claims.Infrastructure.Processing;
using WorkSafeBC.Claims.Infrastructure.Secrets;
using WorkSafeBC.Claims.Infrastructure.Storage;

namespace WorkSafeBC.Claims.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClaimsStorageOptions>(configuration.GetSection(ClaimsStorageOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInboundClaimFileReader, BlobInboundClaimFileReader>();
        services.AddSingleton<IClaimFileParser, ClaimFileParser>();
        services.AddSingleton<IClaimMessagePublisher, RabbitMqClaimMessagePublisher>();
        services.AddSingleton<IClaimProcessingLedger, InMemoryClaimProcessingLedger>();
        services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();

        services.AddSingleton(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClaimsStorageOptions>>().Value;
            return new BlobContainerClient(options.ConnectionString, options.ContainerName);
        });

        services.AddSingleton(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeyVaultOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.VaultUri)
                ? null
                : new SecretClient(new Uri(options.VaultUri), new DefaultAzureCredential());
        });

        return services;
    }
}
