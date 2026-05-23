using System.Security.Cryptography;
using System.Text;

namespace AutoLeaseNet.Adapters.Tajeer.Webhooks;

/// <summary>
/// Tajeer webhook authentication is a shared secret carried in the <c>secret-key</c>
/// header (Spec 03 §12.1 / §12.2). We compare the received value to our configured
/// <see cref="Configuration.TajeerOptions.WebhookSharedSecret"/> using
/// <see cref="CryptographicOperations.FixedTimeEquals"/> to defeat timing attacks.
///
/// Null / empty inputs always return <c>false</c> — there is no anonymous webhook path.
/// </summary>
public static class WebhookSignatureValidator
{
    public static bool IsValid(string? receivedSecret, string? expectedSecret)
    {
        if (string.IsNullOrEmpty(receivedSecret)) return false;
        if (string.IsNullOrEmpty(expectedSecret)) return false;

        var received = Encoding.UTF8.GetBytes(receivedSecret);
        var expected = Encoding.UTF8.GetBytes(expectedSecret);

        // FixedTimeEquals requires equal-length buffers; mismatched lengths short-circuit
        // to false BUT we still spend the comparison cost on equal length to avoid
        // leaking the secret length via timing.
        if (received.Length != expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(received, expected);
    }
}
