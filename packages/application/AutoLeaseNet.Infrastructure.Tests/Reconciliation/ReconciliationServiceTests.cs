using AutoLeaseNet.Infrastructure.Reconciliation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Reconciliation;

/// <summary>
/// Behavioural contract for <see cref="ReconciliationService"/>. We exercise
/// <c>RunCycleAsync</c> directly (one cycle, deterministic) rather than booting
/// the BackgroundService loop. Mirror of the OutboxDrainService test pattern.
/// </summary>
public sealed class ReconciliationServiceTests
{
    [Fact]
    public async Task RunCycleAsync_invokes_every_registered_check_once()
    {
        var check1 = NewCheck("check-a");
        var check2 = NewCheck("check-b");

        var service = NewService(new[] { check1, check2 }, out _);

        await service.RunCycleAsync(CancellationToken.None);

        await check1.Received(1).RunAsync(Arg.Any<CancellationToken>());
        await check2.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCycleAsync_when_a_check_throws_still_runs_the_remaining_checks()
    {
        var failing = NewCheck("failing");
        failing
            .When(c => c.RunAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("boom"));
        var healthy = NewCheck("healthy");

        var service = NewService(new[] { failing, healthy }, out _);

        Func<Task> act = () => service.RunCycleAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(because: "ReconciliationService isolates each check");
        await healthy.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCycleAsync_with_no_checks_registered_is_a_silent_noop()
    {
        var service = NewService(Array.Empty<IReconciliationCheck>(), out _);

        Func<Task> act = () => service.RunCycleAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_when_disabled_returns_immediately_without_running_any_check()
    {
        var check = NewCheck("never");
        var opts = NewOptions();
        opts.Enabled = false;
        var service = NewService(new[] { check }, out _, opts);

        // ExecuteAsync is protected; trigger it via StartAsync/StopAsync.
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50); // long enough for ExecuteAsync to return
        await service.StopAsync(CancellationToken.None);

        await check.DidNotReceive().RunAsync(Arg.Any<CancellationToken>());
    }

    private static IReconciliationCheck NewCheck(string name)
    {
        var c = Substitute.For<IReconciliationCheck>();
        c.Name.Returns(name);
        c.RunAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return c;
    }

    private static ReconciliationOptions NewOptions() => new()
    {
        Enabled = true,
        IntervalSeconds = 900,
        JitterSeconds = 0,
    };

    private static ReconciliationService NewService(
        IReconciliationCheck[] checks,
        out IServiceProvider sp,
        ReconciliationOptions? optionsValue = null)
    {
        var services = new ServiceCollection();
        foreach (var c in checks) services.AddSingleton(c);
        sp = services.BuildServiceProvider();
        var opts = Options.Create(optionsValue ?? NewOptions());
        return new ReconciliationService(sp, opts, NullLogger<ReconciliationService>.Instance);
    }
}
