using System.Net;
using AutoLeaseNet.Adapters.Tajeer.Authentication;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

public sealed class TajeerAuthHandlerTests
{
    private static TajeerOptions DefaultOptions() => new()
    {
        BaseUrl = "https://tajeer-stg.api.elm.sa",
        IssuanceUrlBase = "https://tajeerstg.logisti.sa",
        AppId = "test-app-id",
        AppKey = "test-app-key",
        AuthorizationToken = "Basic test-token-xyz",
        BranchId = 1,
        TimeoutSeconds = 30,
        WebhookSharedSecret = "secret",
        IsSandbox = true,
    };

    private sealed class CapturingInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<HttpRequestMessage> SendAndCapture(TajeerOptions options)
    {
        var capturing = new CapturingInnerHandler();
        var handler = new TajeerAuthHandler(new OptionsMonitorStub(options))
        {
            InnerHandler = capturing,
        };
        var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://tajeer-stg.api.elm.sa/api/lookups/branches"),
            CancellationToken.None);
        return capturing.CapturedRequest!;
    }

    // T3.2 — RED: fails because SendAsync throws NotImplementedException.
    // T3.3 — GREEN: handler injects all three headers.
    [Fact]
    public async Task SendAsync_injects_App_id_App_key_Authorization_headers()
    {
        var sent = await SendAndCapture(DefaultOptions());

        sent.Headers.GetValues("App-id").Should().ContainSingle().Which.Should().Be("test-app-id");
        sent.Headers.GetValues("App-key").Should().ContainSingle().Which.Should().Be("test-app-key");
        sent.Headers.GetValues("Authorization").Should().ContainSingle().Which.Should().Be("Basic test-token-xyz");
    }

    [Fact]
    public async Task SendAsync_does_not_overwrite_existing_correlation_or_content_headers()
    {
        var capturing = new CapturingInnerHandler();
        var handler = new TajeerAuthHandler(new OptionsMonitorStub(DefaultOptions()))
        {
            InnerHandler = capturing,
        };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://tajeer-stg.api.elm.sa/api/contracts")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Correlation-Id", "trace-abc");

        await invoker.SendAsync(request, CancellationToken.None);

        capturing.CapturedRequest!.Headers.GetValues("X-Correlation-Id").Single().Should().Be("trace-abc");
        capturing.CapturedRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_re_reads_options_on_each_call_so_token_rotation_is_picked_up()
    {
        var initial = DefaultOptions();
        var rotated = new TajeerOptions
        {
            BaseUrl = initial.BaseUrl,
            IssuanceUrlBase = initial.IssuanceUrlBase,
            AppId = initial.AppId,
            AppKey = initial.AppKey,
            AuthorizationToken = "Basic rotated-token",
            BranchId = initial.BranchId,
            TimeoutSeconds = initial.TimeoutSeconds,
            WebhookSharedSecret = initial.WebhookSharedSecret,
            IsSandbox = initial.IsSandbox,
        };
        var monitor = new OptionsMonitorStub(initial);

        var capturing = new CapturingInnerHandler();
        var handler = new TajeerAuthHandler(monitor) { InnerHandler = capturing };
        var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://x/1"), CancellationToken.None);
        capturing.CapturedRequest!.Headers.GetValues("Authorization").Single().Should().Be("Basic test-token-xyz");

        monitor.Current = rotated;
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://x/2"), CancellationToken.None);
        capturing.CapturedRequest!.Headers.GetValues("Authorization").Single().Should().Be("Basic rotated-token");
    }

    private sealed class OptionsMonitorStub(TajeerOptions initial) : IOptionsMonitor<TajeerOptions>
    {
        public TajeerOptions Current { get; set; } = initial;
        public TajeerOptions CurrentValue => Current;
        public TajeerOptions Get(string? name) => Current;
        public IDisposable? OnChange(Action<TajeerOptions, string?> listener) => null;
    }
}
