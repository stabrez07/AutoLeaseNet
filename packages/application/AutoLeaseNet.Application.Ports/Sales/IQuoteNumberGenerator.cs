namespace AutoLeaseNet.Application.Ports.Sales;

/// <summary>
/// Port for generating unique, human-readable quotation numbers.
/// Default implementation (Infrastructure): <c>Q-{yyyyMMdd}-{sequence:D4}</c>.
/// </summary>
public interface IQuoteNumberGenerator
{
    /// <summary>Returns a new unique quote number scoped to <paramref name="tenantId"/>.</summary>
    Task<string> GenerateAsync(Guid tenantId, CancellationToken ct);
}
