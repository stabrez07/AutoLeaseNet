namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Discriminator for an <see cref="Inspection"/> per Spec 01 §5.6. PRE_DELIVERY runs
/// before any contract exists; CHECK_OUT is the at-delivery sketch + photos that the
/// Lease Issuance Saga requires; CHECK_IN is the at-return sketch that closes the
/// contract; INCIDENT is ad-hoc damage reporting; PERIODIC covers the regulatory MVPI
/// cycle; CHECK_OUT_CORRECTION supersedes a finished CHECK_OUT when ops needs to fix
/// data without breaking the immutability invariant on the original.
/// </summary>
public enum InspectionType
{
    PreDelivery = 1,
    CheckOut = 2,
    CheckIn = 3,
    Incident = 4,
    Periodic = 5,
    CheckOutCorrection = 6,
}
