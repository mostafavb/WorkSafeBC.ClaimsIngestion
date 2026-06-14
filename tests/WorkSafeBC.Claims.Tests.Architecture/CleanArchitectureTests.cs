using FluentAssertions;
using NetArchTest.Rules;
using WorkSafeBC.Claims.Domain.Entities;

namespace WorkSafeBC.Claims.Tests.Architecture;

public sealed class CleanArchitectureTests
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure_Packages()
    {
        var result = Types
            .InAssembly(typeof(InjuryClaim).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Azure", "RabbitMQ", "OpenTelemetry", "Microsoft.Extensions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Worker_Or_Infrastructure()
    {
        var result = Types
            .InAssembly(typeof(WorkSafeBC.Claims.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("WorkSafeBC.Claims.Infrastructure", "WorkSafeBC.Claims.Worker")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
