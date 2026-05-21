using System.Diagnostics.CodeAnalysis;

namespace AutoLeaseNet.Adapters.Common.Result;

/// <summary>
/// Shared result type for all Pattern B adapters (Tajeer, ZATCA, D365, etc.).
/// Per Spec 04 §15 Q3 — standardized across adapters.
///
/// Carries success value OR error metadata (code, message, transient flag, correlation id).
/// IsTransient distinguishes retryable errors (5xx, timeout, network) from permanent ones
/// (4xx business rule, validation).
/// </summary>
/// <typeparam name="T">Payload type for successful results.</typeparam>
[SuppressMessage(
    "Microsoft.Design",
    "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
    Justification = "Result<T>.Success(value) / Failure(...) is the idiomatic factory pattern for generic result types (LanguageExt, FluentResults). A non-generic helper class would obscure the payload type at call sites.")]
public sealed record IntegrationResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public bool IsTransient { get; private init; }
    public string? CorrelationId { get; private init; }

    /// <summary>Factory: successful result carrying a value.</summary>
    public static IntegrationResult<T> Success(T value, string? correlationId = null) => new()
    {
        IsSuccess = true,
        Value = value,
        CorrelationId = correlationId,
    };

    /// <summary>Factory: failed result with explicit transient classification.</summary>
    public static IntegrationResult<T> Failure(
        string errorCode,
        string errorMessage,
        bool isTransient = false,
        string? correlationId = null) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        IsTransient = isTransient,
        CorrelationId = correlationId,
    };
}

/// <summary>Marker type for void-returning operations (use IntegrationResult&lt;Unit&gt;).</summary>
public sealed record Unit
{
    public static readonly Unit Value = new();
}
