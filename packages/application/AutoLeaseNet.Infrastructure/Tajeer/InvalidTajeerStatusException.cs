using System.Globalization;

namespace AutoLeaseNet.Infrastructure.Tajeer;

/// <summary>
/// Thrown by <see cref="TajeerStatusMapper.FromTajeer"/> when the
/// (status, suspension, closure) triple doesn't match any documented Tajeer combination.
/// Caught by the reconciliation cycle (per-check try/catch) and logged so unknown vendor
/// states never crash the loop.
/// </summary>
public sealed class InvalidTajeerStatusException : Exception
{
    public int ContractStatusCode { get; }
    public int? SuspensionReasonCode { get; }
    public int? ClosureReasonCode { get; }

    public InvalidTajeerStatusException(int contractStatusCode, int? suspensionReasonCode, int? closureReasonCode)
        : base(BuildMessage(contractStatusCode, suspensionReasonCode, closureReasonCode))
    {
        ContractStatusCode = contractStatusCode;
        SuspensionReasonCode = suspensionReasonCode;
        ClosureReasonCode = closureReasonCode;
    }

    private static string BuildMessage(int contractStatusCode, int? suspensionReasonCode, int? closureReasonCode)
    {
        var s = suspensionReasonCode is { } sus ? sus.ToString(CultureInfo.InvariantCulture) : "null";
        var c = closureReasonCode is { } cls ? cls.ToString(CultureInfo.InvariantCulture) : "null";
        return $"Unrecognised Tajeer status triple: contractStatusCode={contractStatusCode.ToString(CultureInfo.InvariantCulture)}, suspensionReasonCode={s}, closureReasonCode={c}.";
    }
}
