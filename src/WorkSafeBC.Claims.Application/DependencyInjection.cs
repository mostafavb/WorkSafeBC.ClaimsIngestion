using Microsoft.Extensions.DependencyInjection;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Application.Validation;

namespace WorkSafeBC.Claims.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClaimBusinessValidator, ClaimBusinessValidator>();
        services.AddSingleton<ICommandHandler<ProcessClaimFileCommand, ProcessClaimFileResult>, ProcessClaimFileHandler>();

        return services;
    }
}
