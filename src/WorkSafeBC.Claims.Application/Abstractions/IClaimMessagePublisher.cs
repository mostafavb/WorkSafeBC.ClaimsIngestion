using WorkSafeBC.Claims.Application.Contracts;

namespace WorkSafeBC.Claims.Application.Abstractions;

public interface IClaimMessagePublisher
{
    Task PublishAsync(ClaimIngestionEvent integrationEvent, CancellationToken cancellationToken);
}
