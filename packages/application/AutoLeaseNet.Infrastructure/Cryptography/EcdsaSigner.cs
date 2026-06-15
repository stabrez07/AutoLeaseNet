using System.Security.Cryptography;
using System.Text;

namespace AutoLeaseNet.Infrastructure.Cryptography;

/// <summary>
/// ECDSA P-256 signer for ZATCA invoice clearance per Spec 02 §4.5.
/// Signs canonical UBL XML with private key; embeds signature in signed-xml structure.
/// Phase 1: private key from appsettings (Phase 2: Azure Key Vault).
/// </summary>
public sealed class EcdsaSigner
{
    private readonly ECDsa _privateKey;
    private readonly string _certificateThumbprint;

    /// <summary>Initialize signer with private key (PEM format) and certificate thumbprint.</summary>
    /// <param name="privateKeyPem">ECDSA P-256 private key in PEM format (-----BEGIN EC PRIVATE KEY-----).</param>
    /// <param name="certificateThumbprint">SHA-256 thumbprint of signing certificate (hex-encoded).</param>
    public EcdsaSigner(string privateKeyPem, string certificateThumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateThumbprint);

        _privateKey = LoadEcdsaPrivateKey(privateKeyPem);
        _certificateThumbprint = certificateThumbprint;
    }

    /// <summary>Sign canonical UBL XML. Returns signature in base64 format (ready for XML embedding).</summary>
    public string SignUblXml(string canonicalUbl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalUbl);

        var ublBytes = Encoding.UTF8.GetBytes(canonicalUbl);

        // Sign data with ECDSA P-256 (uses SHA-256 internally)
        var signatureBytes = _privateKey.SignData(ublBytes, HashAlgorithmName.SHA256);

        // Return base64-encoded signature
        return Convert.ToBase64String(signatureBytes);
    }

    /// <summary>Verify signature (for testing). Returns true if signature valid.</summary>
    public bool VerifySignature(string canonicalUbl, string signatureBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalUbl);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureBase64);

        try
        {
            var ublBytes = Encoding.UTF8.GetBytes(canonicalUbl);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            return _privateKey.VerifyData(ublBytes, signatureBytes, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Load ECDSA P-256 private key from PEM format.</summary>
    private static ECDsa LoadEcdsaPrivateKey(string privateKeyPem)
    {
        // Remove PEM headers/footers and whitespace
        var keyData = privateKeyPem
            .Replace("-----BEGIN EC PRIVATE KEY-----", string.Empty)
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
            .Replace("-----END EC PRIVATE KEY-----", string.Empty)
            .Replace("-----END PRIVATE KEY-----", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\r", string.Empty)
            .Trim();

        var keyBytes = Convert.FromBase64String(keyData);

        // Import as PKCS#8 or SEC1 (EC) format
        var ecdsa = ECDsa.Create();
        try
        {
            // Try PKCS#8 first
            ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);
        }
        catch
        {
            // Fall back to SEC1 (raw EC private key)
            ecdsa.ImportECPrivateKey(keyBytes, out _);
        }

        return ecdsa;
    }

    public void Dispose()
    {
        _privateKey?.Dispose();
    }
}
