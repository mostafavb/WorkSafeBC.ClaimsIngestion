using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Domain.Entities;

namespace WorkSafeBC.Claims.Application.Abstractions;

public interface IClaimFileParser
{
    IReadOnlyCollection<InjuryClaim> Parse(InboundClaimFile file);
}
