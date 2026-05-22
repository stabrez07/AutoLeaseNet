using AutoLeaseNet.Bff.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Authentication;

/// <summary>
/// T2.1 EXTRA + T2.6 — DevJwtStubHandler MUST NOT be registered in Production.
/// AddDevJwtStub throws InvalidOperationException; WebApplicationFactory amplifies that
/// into a host-start failure (proving Program.cs is wired correctly).
/// </summary>
public sealed class DevJwtStubProductionGuardTests
{
    [Fact]
    public void AddDevJwtStub_throws_when_environment_is_Production()
    {
        var factory = new ProductionWebApplicationFactory();

        Action act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(DevJwtStubHandler)}*");
    }

    [Fact]
    public void AddDevJwtStub_succeeds_in_Staging()
    {
        // Staging is non-Production → allowed (CI / pre-prod testing).
        var factory = new EnvironmentWebApplicationFactory("Staging");

        Action act = () => factory.CreateClient();

        act.Should().NotThrow();
    }

    private sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Production");
    }

    private sealed class EnvironmentWebApplicationFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment(environment);
    }
}
