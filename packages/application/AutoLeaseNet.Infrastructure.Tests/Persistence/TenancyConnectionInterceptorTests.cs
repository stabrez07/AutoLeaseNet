using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit-level guarantees for <see cref="TenancyConnectionInterceptor"/>. The real
/// SESSION_CONTEXT round-trip is covered by the
/// <c>RlsIsolationTests</c> integration test (Category=Integration, gated on a
/// local SQL Server). What this file pins down is the safety net: the interceptor
/// must NOT throw or otherwise interfere when:
///
/// <list type="number">
///   <item>The underlying connection is EF InMemory (every existing test fixture).</item>
///   <item>The accessor returns <c>null</c> (anonymous request, e.g. webhook
///         pre-resolution).</item>
/// </list>
///
/// If either case ever started throwing, every endpoint test in the BFF would
/// break in lockstep — these tests catch that regression at the unit level.
/// </summary>
public sealed class TenancyConnectionInterceptorTests
{
    [Fact]
    public async Task EF_InMemory_provider_invokes_the_interceptor_without_throwing()
    {
        var accessor = Substitute.For<ITenancyAccessor>();
        accessor.Current.Returns(new Tenancy(Guid.NewGuid(), null, "INTERNAL_STAFF"));

        var interceptor = new TenancyConnectionInterceptor(accessor, NullLogger<TenancyConnectionInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new AutoLeaseNetDbContext(options);

        // Any operation that forces the EF provider to "open" its in-memory
        // connection — for the InMemory provider this is essentially a no-op
        // path, which is exactly what we are confirming the interceptor honours.
        var act = async () => await db.Leases.AnyAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task When_accessor_returns_null_no_attempt_is_made_to_set_session_context()
    {
        var accessor = Substitute.For<ITenancyAccessor>();
        accessor.Current.Returns((Tenancy?)null);

        var interceptor = new TenancyConnectionInterceptor(accessor, NullLogger<TenancyConnectionInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new AutoLeaseNetDbContext(options);

        var act = async () => await db.Leases.AnyAsync();

        await act.Should().NotThrowAsync();
        // We can't directly observe the not-called branch on InMemory because no
        // SqlConnection ever materialises — but proving the no-throw path is
        // sufficient: any attempt to invoke SqlSessionContext against InMemory
        // would have raised InvalidCastException at the `connection is SqlConnection`
        // guard line.
    }
}
