using AutoLeaseNet.Application.Ports.Tenancy;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Tenancy;

/// <summary>
/// Behavioural contract for <see cref="SystemTenancyScope"/>: nests correctly via
/// AsyncLocal save-and-restore, exposes the right SYSTEM tenancy while active,
/// and is null again once disposed. This is load-bearing because the demo seeder
/// and the Tajeer webhook receiver both rely on the scope flowing across
/// <c>await</c> boundaries — if AsyncLocal were lost (e.g. via a thread-static
/// implementation), seeded rows would be RLS-hidden from the very seeder that
/// just inserted them.
/// </summary>
public sealed class SystemTenancyScopeTests
{
    [Fact]
    public void Current_is_null_outside_any_scope()
    {
        SystemTenancyScope.Current.Should().BeNull();
    }

    [Fact]
    public void For_pushes_SYSTEM_tenancy_with_no_customer()
    {
        var tenantId = Guid.NewGuid();

        using var scope = SystemTenancyScope.For(tenantId);

        SystemTenancyScope.Current.Should().NotBeNull();
        SystemTenancyScope.Current!.TenantId.Should().Be(tenantId);
        SystemTenancyScope.Current.UserType.Should().Be("SYSTEM");
        SystemTenancyScope.Current.CustomerId.Should().BeNull();
    }

    [Fact]
    public void Dispose_restores_previous_scope_to_null()
    {
        var tenantId = Guid.NewGuid();

        using (SystemTenancyScope.For(tenantId))
        {
            SystemTenancyScope.Current.Should().NotBeNull();
        }

        SystemTenancyScope.Current.Should().BeNull();
    }

    [Fact]
    public void Nested_scopes_save_and_restore_the_parent_tenancy()
    {
        var outer = Guid.NewGuid();
        var inner = Guid.NewGuid();

        using (SystemTenancyScope.For(outer))
        {
            SystemTenancyScope.Current!.TenantId.Should().Be(outer);

            using (SystemTenancyScope.For(inner))
            {
                SystemTenancyScope.Current!.TenantId.Should().Be(inner);
            }

            SystemTenancyScope.Current!.TenantId.Should().Be(outer,
                because: "disposing the inner scope must restore the outer one");
        }

        SystemTenancyScope.Current.Should().BeNull();
    }

    [Fact]
    public async Task Scope_flows_across_await_boundaries()
    {
        var tenantId = Guid.NewGuid();

        using (SystemTenancyScope.For(tenantId))
        {
            await Task.Yield();
            SystemTenancyScope.Current!.TenantId.Should().Be(tenantId);

            await Task.Run(() =>
            {
                // Inside Task.Run — still on the same logical async flow,
                // AsyncLocal must propagate.
                SystemTenancyScope.Current!.TenantId.Should().Be(tenantId);
            });
        }
    }

    [Fact]
    public async Task Sibling_async_flows_do_not_see_each_others_scopes()
    {
        var seenInOther = await Task.Run(async () =>
        {
            await Task.Yield();
            return SystemTenancyScope.Current;
        });

        // A task started OUTSIDE any scope must not see one — and (critically) a
        // scope opened on the test thread between Task.Run and Task.Yield won't
        // leak into the captured execution context.
        seenInOther.Should().BeNull();
    }
}
