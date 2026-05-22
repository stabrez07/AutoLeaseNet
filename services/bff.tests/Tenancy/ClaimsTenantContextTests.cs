using System.Security.Claims;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using AutoLeaseNet.Bff.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Tenancy;

public sealed class ClaimsTenantContextTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid User = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ITenantContext BuildSut(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        // NOTE: the real HttpContextAccessor backs HttpContext with AsyncLocal, so two
        // BuildSut calls in the same async flow would clobber each other. Stub gives each
        // ClaimsTenantContext an isolated HttpContext.
        var accessor = new StubHttpContextAccessor(httpContext);
        return new ClaimsTenantContext(accessor);
    }

    private sealed class StubHttpContextAccessor(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }

    [Fact]
    public void TenantId_returns_claim_when_present()
    {
        var sut = BuildSut(new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()));

        sut.TenantId.Should().Be(Tenant);
    }

    [Fact]
    public void TenantId_throws_when_claim_missing()
    {
        var sut = BuildSut();

        Action act = () => _ = sut.TenantId;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tenant_id*");
    }

    [Fact]
    public void CustomerId_returns_null_when_claim_absent()
    {
        var sut = BuildSut(new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()));

        sut.CustomerId.Should().BeNull();
    }

    [Fact]
    public void CustomerId_parses_claim_when_present()
    {
        var sut = BuildSut(
            new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()),
            new Claim(DevJwtStubHandler.ClaimCustomerId, Customer.ToString()));

        sut.CustomerId.Should().Be(Customer);
    }

    [Fact]
    public void UserId_reads_NameIdentifier_claim()
    {
        var sut = BuildSut(
            new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()),
            new Claim(ClaimTypes.NameIdentifier, User.ToString()));

        sut.UserId.Should().Be(User);
    }

    [Fact]
    public void UserType_returns_UNKNOWN_when_claim_absent()
    {
        var sut = BuildSut(new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()));

        sut.UserType.Should().Be("UNKNOWN");
    }

    [Fact]
    public void IsInternalStaff_is_true_only_for_INTERNAL_STAFF_user_type()
    {
        var staff = BuildSut(
            new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()),
            new Claim(DevJwtStubHandler.ClaimUserType, "INTERNAL_STAFF"));
        var external = BuildSut(
            new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()),
            new Claim(DevJwtStubHandler.ClaimUserType, "EXTERNAL_FLEET_ADMIN"));

        staff.IsInternalStaff.Should().BeTrue();
        external.IsInternalStaff.Should().BeFalse();
    }

    [Fact]
    public void BranchIds_collects_all_branch_id_claims()
    {
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var sut = BuildSut(
            new Claim(DevJwtStubHandler.ClaimTenantId, Tenant.ToString()),
            new Claim(DevJwtStubHandler.ClaimBranchId, b1.ToString()),
            new Claim(DevJwtStubHandler.ClaimBranchId, b2.ToString()),
            new Claim(DevJwtStubHandler.ClaimBranchId, "not-a-guid"));

        sut.BranchIds.Should().BeEquivalentTo(new[] { b1, b2 });
    }
}
