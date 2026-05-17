using System.Text.RegularExpressions;

namespace AutoLeaseNet.Adapters.Common.Observability;

/// <summary>
/// Shared PII masking for logs. Adapters use this in their LoggingHandler before persisting
/// request/response bodies to logs / integration log table.
/// </summary>
public static partial class PiiMasking
{
    private static readonly HashSet<string> SensitiveJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "idNumber", "passportNumber", "driveLicenseNumber", "iqamaNumber",
        "mobile", "phoneNumber", "email",
        "iban", "accountNumber", "cardNumber",
        "password", "secret", "apiKey", "authorization",
        "ssn", "nin"
    };

    /// <summary>Mask sensitive values in JSON body for safe logging.</summary>
    public static string MaskJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        var masked = json;
        foreach (var key in SensitiveJsonKeys)
        {
            // Match "key": "value" or "key": 123 — replace value
            var pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*(\"[^\"]*\"|\\d+)";
            masked = Regex.Replace(masked, pattern, $"\"{key}\":\"***\"", RegexOptions.IgnoreCase);
        }
        return masked;
    }
}
