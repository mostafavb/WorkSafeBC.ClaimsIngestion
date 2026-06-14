using WorkSafeBC.Claims.Domain.Entities;

namespace WorkSafeBC.Claims.Application.Validation;

public interface IClaimBusinessValidator
{
    void Validate(InjuryClaim claim);
}
