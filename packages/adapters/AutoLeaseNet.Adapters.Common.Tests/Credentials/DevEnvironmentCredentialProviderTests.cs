using System.Diagnostics.CodeAnalysis;
using AutoLeaseNet.Adapters.Common.Credentials;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoLeaseNet.Adapters.Common.Tests.Credentials;

public sealed class DevEnvironmentCredentialProviderTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Tests intentionally exercise the ICredentialProvider abstraction surface.")]
    private static ICredentialProvider Build(IDictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new DevEnvironmentCredentialProvider(config);
    }

    // T1.9 — global secrets resolve under "Secrets:{name}".
    [Fact]
    public async Task GetAsync_returns_global_secret_from_configuration()
    {
        var sut = Build(new Dictionary<string, string?>
        {
            ["Secrets:WebhookSharedKey"] = "local-dev-secret-xyz",
        });

        var value = await sut.GetAsync("WebhookSharedKey");

        value.Should().Be("local-dev-secret-xyz");
    }

    [Fact]
    public async Task GetAsync_returns_null_when_secret_missing()
    {
        var sut = Build(new Dictionary<string, string?>());

        var value = await sut.GetAsync("Nonexistent");

        value.Should().BeNull();
    }

    // T1.9 — per-tenant secrets resolve under "Tenants:{tenantId}:{adapter}:{key}".
    [Fact]
    public async Task GetTenantSecretAsync_returns_per_tenant_value()
    {
        var sut = Build(new Dictionary<string, string?>
        {
            [$"Tenants:{TenantA}:tajeer:app-key"] = "tenant-A-tajeer-app-key",
            [$"Tenants:{TenantA}:tajeer:authorization"] = "Basic abc123",
        });

        (await sut.GetTenantSecretAsync("tajeer", TenantA, "app-key"))
            .Should().Be("tenant-A-tajeer-app-key");
        (await sut.GetTenantSecretAsync("tajeer", TenantA, "authorization"))
            .Should().Be("Basic abc123");
    }

    [Fact]
    public async Task GetTenantSecretAsync_isolates_per_tenant()
    {
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var sut = Build(new Dictionary<string, string?>
        {
            [$"Tenants:{TenantA}:tajeer:app-key"] = "tenant-A-key",
            [$"Tenants:{tenantB}:tajeer:app-key"] = "tenant-B-key",
        });

        (await sut.GetTenantSecretAsync("tajeer", TenantA, "app-key"))
            .Should().Be("tenant-A-key");
        (await sut.GetTenantSecretAsync("tajeer", tenantB, "app-key"))
            .Should().Be("tenant-B-key");
    }

    [Fact]
    public void Constructor_throws_when_configuration_is_null()
    {
        Action act = () => _ = new DevEnvironmentCredentialProvider(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
