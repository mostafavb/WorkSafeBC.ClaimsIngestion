using FluentAssertions;
using NetArchTest.Rules;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Domain.Entities;
using WorkSafeBC.Claims.Infrastructure;
using WorkSafeBC.Claims.Infrastructure.Messaging;
using WorkSafeBC.Claims.Infrastructure.Storage;
using WorkSafeBC.Claims.Worker;

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
    public void Domain_Should_Not_Depend_On_Application_Or_Worker_Assemblies()
    {
        var result = Types
            .InAssembly(typeof(InjuryClaim).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "WorkSafeBC.Claims.Application",
                "WorkSafeBC.Claims.Infrastructure",
                "WorkSafeBC.Claims.Worker")
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

    [Fact]
    public void Application_Handlers_Should_Implement_CommandHandler_Contract()
    {
        var result = Types
            .InAssembly(typeof(ProcessClaimFileHandler).Assembly)
            .That()
            .ResideInNamespace("WorkSafeBC.Claims.Application.Claims")
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Worker()
    {
        var result = Types
            .InAssembly(typeof(DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("WorkSafeBC.Claims.Worker")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_Adapters_Should_Implement_Application_Interfaces()
    {
        Types
            .InAssembly(typeof(DependencyInjection).Assembly)
            .That()
            .HaveName("BlobInboundClaimFileReader")
            .Should()
            .ImplementInterface(typeof(IInboundClaimFileReader))
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue();

        Types
            .InAssembly(typeof(DependencyInjection).Assembly)
            .That()
            .HaveName("ClaimFileParser")
            .Should()
            .ImplementInterface(typeof(IClaimFileParser))
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue();

        Types
            .InAssembly(typeof(DependencyInjection).Assembly)
            .That()
            .HaveName("RabbitMqClaimMessagePublisher")
            .Should()
            .ImplementInterface(typeof(IClaimMessagePublisher))
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Worker_Should_Not_Reference_Infrastructure_Implementation_Namespaces_Directly()
    {
        var result = Types
            .InAssembly(typeof(ClaimsIngestionWorker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "WorkSafeBC.Claims.Infrastructure.Storage",
                "WorkSafeBC.Claims.Infrastructure.Messaging",
                "WorkSafeBC.Claims.Infrastructure.Processing")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
