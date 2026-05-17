namespace AutoLeaseNet.Adapters.Common.Result;

/// <summary>
/// Shared discriminated-union result for all Pattern B adapters (Tajeer, ZATCA, D365, etc.).
/// Per doc 04 §15 Q3 — standardize across adapters; vendor-specific errors extend IntegrationError.
/// </summary>
public abstract record IntegrationResult<T>
{
    public sealed record Ok(T Value) : IntegrationResult<T>;
    public sealed record BusinessError(IntegrationError Error) : IntegrationResult<T>;
    public sealed record SystemError(string Message, Exception? Exception = null) : IntegrationResult<T>;

    public bool IsOk => this is Ok;
    public T? ValueOrDefault => this is Ok ok ? ok.Value : default;

    public IntegrationResult<TOut> Map<TOut>(Func<T, TOut> mapper) => this switch
    {
        Ok ok => new IntegrationResult<TOut>.Ok(mapper(ok.Value)),
        BusinessError be => new IntegrationResult<TOut>.BusinessError(be.Error),
        SystemError se => new IntegrationResult<TOut>.SystemError(se.Message, se.Exception),
        _ => throw new InvalidOperationException("Unknown IntegrationResult variant")
    };
}

/// <summary>Base for vendor-specific errors. Tajeer/ZATCA/etc. extend with their own error codes.</summary>
public abstract record IntegrationError(string Code, string RawMessage, LocalizedMessage UserMessage, ErrorCategory Category);

public sealed record LocalizedMessage(string Ar, string En);

public enum ErrorCategory
{
    Validation,
    BusinessRule,
    Authorization,
    ExternalDependency,
    SystemError,
    NotFound,
    Conflict,
    RateLimited
}

/// <summary>Marker type for void-returning operations.</summary>
public sealed record Unit
{
    public static readonly Unit Value = new();
}
