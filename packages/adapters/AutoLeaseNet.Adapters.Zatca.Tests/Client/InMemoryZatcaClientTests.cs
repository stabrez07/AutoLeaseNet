using AutoLeaseNet.Adapters.Zatca.Configuration;
using AutoLeaseNet.Adapters.Zatca.Dtos;
using AutoLeaseNet.Adapters.Zatca.InMemory;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Zatca.Tests.Client;

/// <summary>
/// Behavioural contract for <see cref="InMemoryZatcaClient"/>. The fake is the
/// primary tool the saga tests (Week-4) will use to drive cleared / rejected scenarios
/// without ever touching the Fatoorah sandbox — so the behaviours pinned here are the
/// ones those tests will rely on.
/// </summary>
public sealed class InMemoryZatcaClientTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

    private static SubmitInvoiceRequest NewRequest(Guid? uuid = null) => new(
        Uuid: uuid ?? Guid.NewGuid(),
        InvoiceType: ZatcaInvoiceType.Tax,
        InvoiceXml: "<Invoice/>",
        InvoiceHash: "hash-current",
        PreviousInvoiceHash: "hash-previous");

    [Fact]
    public async Task SubmitInvoiceAsync_returns_Cleared_by_default()
    {
        var client = new InMemoryZatcaClient(() => T0);
        var req = NewRequest();

        var result = await client.SubmitInvoiceAsync(req);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Uuid.Should().Be(req.Uuid);
        result.Value.Status.Should().Be(ZatcaResultStatus.Cleared);
        result.Value.ClearedAtUtc.Should().Be(T0);
        result.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedRejection_makes_the_next_submit_for_that_uuid_return_Rejected_with_no_cleared_at()
    {
        var client = new InMemoryZatcaClient(() => T0);
        var req = NewRequest();
        client.SeedRejection(req.Uuid);

        var result = await client.SubmitInvoiceAsync(req);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ZatcaResultStatus.Rejected);
        result.Value.ClearedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task SubmitCalls_records_every_invocation_in_order()
    {
        var client = new InMemoryZatcaClient(() => T0);
        var a = NewRequest();
        var b = NewRequest();
        var c = NewRequest();

        await client.SubmitInvoiceAsync(a);
        await client.SubmitInvoiceAsync(b);
        await client.SubmitInvoiceAsync(c);

        client.SubmitCalls.Should().HaveCount(3);
        client.SubmitCalls[0].Uuid.Should().Be(a.Uuid);
        client.SubmitCalls[1].Uuid.Should().Be(b.Uuid);
        client.SubmitCalls[2].Uuid.Should().Be(c.Uuid);
    }

    [Fact]
    public async Task SubmitInvoiceAsync_is_idempotent_on_uuid()
    {
        var client = new InMemoryZatcaClient(() => T0);
        var req = NewRequest();

        var first = await client.SubmitInvoiceAsync(req);
        var second = await client.SubmitInvoiceAsync(req);

        // Same response object shape (same Uuid + Status + ClearedAtUtc).
        second.Value!.Should().BeEquivalentTo(first.Value);
        // But the call IS recorded each time — the adapter just returns the cached response.
        client.SubmitCalls.Should().HaveCount(2);
    }
}
