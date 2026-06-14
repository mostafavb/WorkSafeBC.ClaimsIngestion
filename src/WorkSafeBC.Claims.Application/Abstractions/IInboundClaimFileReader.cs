using WorkSafeBC.Claims.Application.Claims;

namespace WorkSafeBC.Claims.Application.Abstractions;

public interface IInboundClaimFileReader
{
    Task<IReadOnlyCollection<InboundClaimFile>> GetPendingFilesAsync(CancellationToken cancellationToken);
}
