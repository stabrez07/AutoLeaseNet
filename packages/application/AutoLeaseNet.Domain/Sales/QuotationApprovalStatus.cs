namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Decision state of a single <see cref="QuotationApproval"/> tier row (Spec 01 §5.4).
/// Numeric values are stable persisted contract; never renumber.
/// </summary>
public enum QuotationApprovalStatus
{
    /// <summary>Awaiting a decision from the required role / assigned approver.</summary>
    Pending = 1,

    /// <summary>Approved by this tier.</summary>
    Approved = 2,

    /// <summary>Rejected by this tier — fails the whole quote.</summary>
    Rejected = 3,

    /// <summary>Cancelled because the sales rep recalled the quote before this tier decided.</summary>
    Recalled = 4,
}
