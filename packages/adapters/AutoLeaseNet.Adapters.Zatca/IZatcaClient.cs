namespace AutoLeaseNet.Adapters.Zatca;

/// <summary>
/// ZATCA Phase 2 e-invoicing client. Per doc 07 (placeholder).
/// Methods (to be added per doc 07 when expanded):
/// - SubmitClearanceAsync (B2B real-time)
/// - SubmitReportingAsync (B2C async within 24h)
/// - GetSubmissionStatusAsync
/// - OnboardEgsAsync
/// </summary>
public interface IZatcaClient;
