namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Damage marker categories shown on the Tajeer vehicle-sketch canvas (Spec 01 §5.6
/// <c>InspectionDamageMarker.Type</c>). The string mapping is preserved on the wire
/// (kebab-case) because Tajeer's sketch JSON uses these literals.
/// </summary>
public enum DamageMarkerType
{
    SmallScratch = 1,
    DeepScratch = 2,
    VeryDeepScratch = 3,
    BendInBody = 4,
}
