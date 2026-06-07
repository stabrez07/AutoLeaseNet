namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Lifecycle of a sales <see cref="Quotation"/> (Spec 02 §4.1). Numeric values are
/// stable contract — persisted and shared with the portals; never renumber.
/// </summary>
public enum QuotationStatus
{
    /// <summary>Editable; lines + pricing can change.</summary>
    Draft = 1,

    /// <summary>Submitted; one or more approval tiers outstanding.</summary>
    PendingApproval = 2,

    /// <summary>All required tiers approved (or none were required). Ready to send.</summary>
    Approved = 3,

    /// <summary>Sent to the customer; awaiting their decision.</summary>
    SentToCustomer = 4,

    /// <summary>Customer accepted — triggers lease provisioning. Immutable.</summary>
    Accepted = 5,

    /// <summary>Rejected by an approver tier or by the customer. Terminal.</summary>
    Rejected = 6,

    /// <summary><see cref="Quotation.ValidUntilDate"/> passed before acceptance. Terminal.</summary>
    Expired = 7,

    /// <summary>Recalled by the sales rep before acceptance. Terminal.</summary>
    Withdrawn = 8,
}
