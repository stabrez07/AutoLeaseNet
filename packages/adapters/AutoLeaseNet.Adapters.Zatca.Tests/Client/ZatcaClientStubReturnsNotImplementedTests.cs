using AutoLeaseNet.Adapters.Zatca.Client;
using AutoLeaseNet.Adapters.Zatca.Configuration;
using AutoLeaseNet.Adapters.Zatca.Dtos;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Adapters.Zatca.Tests.Client;

/// <summary>
/// Pins the Phase-1 contract for the real <see cref="ZatcaClient"/>: any Submit call
/// returns <see cref="ZatcaClient.ErrorCodeNotImplemented"/> as a non-transient failure.
///
/// <para>
/// Why this test exists: the Real client is the default composition (<see cref="ZatcaMode.Real"/>),
/// so a production env that forgot to set <c>Zatca:Mode=InMemory</c> for staging tests
/// would silently call this. Asserting the error code here means any future change that
/// accidentally restores a NotImplementedException, swallows the failure, or returns
/// fake success will break the build.
/// </para>
/// </summary>
public sealed class ZatcaClientStubReturnsNotImplementedTests
{
    [Fact]
    public async Task SubmitInvoiceAsync_returns_not_yet_implemented_failure()
    {
        // The stub never touches HttpClient, so a null-ish factory is fine for the contract test.
        var client = new ZatcaClient(NoopHttpClientFactory.Instance, NullLogger<ZatcaClient>.Instance);
        var req = new SubmitInvoiceRequest(
            Uuid: Guid.NewGuid(),
            InvoiceType: ZatcaInvoiceType.Tax,
            InvoiceXml: "<Invoice/>",
            InvoiceHash: "hash-current",
            PreviousInvoiceHash: "hash-previous");

        var result = await client.SubmitInvoiceAsync(req);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ZatcaClient.ErrorCodeNotImplemented);
        result.IsTransient.Should().BeFalse();
    }

    private sealed class NoopHttpClientFactory : IHttpClientFactory
    {
        public static readonly NoopHttpClientFactory Instance = new();
        public HttpClient CreateClient(string name) => new();
    }
}
