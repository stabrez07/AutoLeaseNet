using System.Text.RegularExpressions;

namespace AutoLeaseNet.Adapters.Common.Observability;

/// <summary>
/// Shared PII masking for logs and integration audit rows. Two APIs:
///
/// - <see cref="Mask(string, string)"/> — field-aware single-value masker; keeps the
///   last 4 chars where useful (ID number, IBAN, license) so support can correlate logs
///   without exposing the full PII.
/// - <see cref="MaskJson(string)"/> — bulk JSON body masker; replaces sensitive values
///   wholesale with "***" before persisting request/response bodies.
/// </summary>
public static partial class PiiMasking
{
    private static readonly HashSet<string> SensitiveJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "idNumber", "passportNumber", "driveLicenseNumber", "iqamaNumber",
        "mobile", "phoneNumber", "email",
        "iban", "accountNumber", "cardNumber",
        "password", "secret", "apiKey", "authorization",
        "ssn", "nin",
    };

    // Fields where we preserve the last N characters so support staff can correlate
    // records without seeing the full PII. Keys are case-insensitive.
    private static readonly Dictionary<string, int> KeepLastChars = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idNumber"] = 4,
        ["iqamaNumber"] = 4,
        ["nin"] = 4,
        ["ssn"] = 4,
        ["passportNumber"] = 4,
        ["driveLicenseNumber"] = 4,
        ["licenseNumber"] = 4,
        ["iban"] = 4,
        ["accountNumber"] = 4,
        ["cardNumber"] = 4,
        ["mobile"] = 4,
        ["phoneNumber"] = 4,
    };

    /// <summary>
    /// Field-aware masking for a single value. Returns input unchanged for null/empty.
    /// For known sensitive fields with a "keep-last-N" policy, preserves the trailing
    /// digits and masks the rest with '*'. For unknown sensitive fields (email, secrets,
    /// passwords), returns "***".
    /// </summary>
    /// <param name="fieldName">Field identifier (case-insensitive). Determines mask strategy.</param>
    /// <param name="value">Raw value to mask.</param>
    /// <returns>Masked string safe for logs.</returns>
    public static string Mask(string fieldName, string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (string.IsNullOrEmpty(fieldName)) return value;

        if (KeepLastChars.TryGetValue(fieldName, out var keep))
        {
            if (value.Length <= keep) return new string('*', value.Length);
            var maskLen = value.Length - keep;
            return new string('*', maskLen) + value[^keep..];
        }

        // Unknown sensitive field — bulk mask
        if (SensitiveJsonKeys.Contains(fieldName)) return "***";

        // Unknown field — assume caller knows it's sensitive (they invoked us) → "***"
        return "***";
    }

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
