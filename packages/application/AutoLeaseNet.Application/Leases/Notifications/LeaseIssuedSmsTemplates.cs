using System.Globalization;
using AutoLeaseNet.Domain.Customers;

namespace AutoLeaseNet.Application.Leases.Notifications;

/// <summary>
/// SMS template renderer for the LeaseIssued notification. Two locales (Ar / En) per
/// CLAUDE.md §UI. Placeholders <c>{contractNumber}</c> and <c>{issuanceUrl}</c> are
/// substituted via straight string replace — no Razor / Liquid for Phase 1.
/// </summary>
public static class LeaseIssuedSmsTemplates
{
    public const string TemplateKeyAr = "lease_issued_ar";
    public const string TemplateKeyEn = "lease_issued_en";

    private const string BodyAr =
        "تم إصدار عقد التأجير رقم {contractNumber} بنجاح. لإكمال الإجراءات يرجى زيارة: {issuanceUrl}";

    private const string BodyEn =
        "Your lease contract {contractNumber} has been issued. Complete the formalities at: {issuanceUrl}";

    /// <summary>Pick the template body for the renter's preferred language and substitute placeholders.</summary>
    public static (string TemplateKey, string Body) Render(
        PreferredLanguage language,
        long contractNumber,
        string issuanceUrl)
    {
        var (key, body) = language switch
        {
            PreferredLanguage.En => (TemplateKeyEn, BodyEn),
            _ => (TemplateKeyAr, BodyAr),
        };
        return (key, body
            .Replace("{contractNumber}", contractNumber.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{issuanceUrl}", issuanceUrl, StringComparison.Ordinal));
    }
}
