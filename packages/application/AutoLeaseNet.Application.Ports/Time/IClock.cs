namespace AutoLeaseNet.Application.Ports.Time;

/// <summary>
/// Abstraction over the system clock to allow deterministic testing.
/// All domain/application code must use IClock, never DateTime.UtcNow directly.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production implementation backed by the OS clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
