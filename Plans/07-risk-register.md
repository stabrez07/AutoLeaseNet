# 07 — Risk Register & Mitigations

**Status**: 📋 Living document — update on every Friday review and after every incident
**Last reviewed**: 2026-05-17

---

## Risk scoring

- **Likelihood**: Low (<10% in next phase), Medium (10-50%), High (>50%)
- **Impact**: Low (delay <1 day), Medium (delay 1 week), High (delay >1 week or compliance issue), Very High (project-blocking)
- **Priority**: Likelihood × Impact

## Active risks

### REG-01 — Tajeer staging API behavior diverges from V9.7 spec

- **Likelihood**: Medium
- **Impact**: High
- **Priority**: High
- **Description**: V9.7 spec is dated Dec 2025; minor undocumented changes may exist; vendor may push V9.8 mid-project
- **Mitigation**:
  - Contract snapshot tests against real sandbox responses
  - Subscribe to Tajeer release notes / mailing list
  - Tolerant readers (ignore unknown fields); strict writers
  - Weekly smoke test of the 12-step demo journey
- **Status**: Mitigated by design; monitor

### TECH-01 — Tajeer webhooks delayed / lost / out-of-order

- **Likelihood**: Medium
- **Impact**: High
- **Priority**: High
- **Mitigation**:
  - Webhook treated as enrichment, not source of truth
  - Reconciliation job every 15 min calling `GET /rent-contract/{contractNumber}` for all non-terminal leases
  - Dedup index on `(Source, ExternalEventId)` in WebhookLog
- **Status**: Mitigated by saga design (Spec 02 §6.2)

### REG-02 — ZATCA PIH chain breaks

- **Likelihood**: Low
- **Impact**: Very High (regulatory non-compliance)
- **Priority**: High
- **Mitigation**:
  - `ZatcaChainState` per tenant; atomic update only on CLEARED
  - Failed submissions do NOT advance chain
  - Chain-break detection on every submit → halt new submissions + alert
  - Daily chain-integrity report
- **Status**: Mitigated by design (Spec 02 §6.6)

### TECH-02 — OpenTelemetry transitive Grpc.Net.Client vulnerability

- **Likelihood**: Certainty (already present in 1.12.0)
- **Impact**: Low (vulnerability is moderate severity; no fix available upstream yet)
- **Priority**: Low
- **Mitigation**: Suppressed via NoWarn `NU1902;NU1903` in `Directory.Build.props` with comment to re-evaluate when upstream fixes
- **Status**: Accepted with monitoring

### PEOPLE-01 — Solo dev burnout / scope creep

- **Likelihood**: High
- **Impact**: High
- **Priority**: Very High
- **Mitigation**:
  - Hard 4-week Phase 1 cutoff
  - Explicit "out of scope" list in [Plan 01 §6](./01-comprehensive-vehicle-lease-customer-portal-plan.md#6-whats-explicitly-out-of-phase-1)
  - TDD discipline via [superpowers framework](https://github.com/obra/superpowers)
  - Weekly checkpoints — defer rather than slip
  - Sustainable pace: no all-nighters; weekend rest mandatory
- **Status**: Mitigated by process

### DEP-01 — Nafath onboarding delays B2C launch

- **Likelihood**: High
- **Impact**: Medium (B2B works without Nafath)
- **Priority**: Medium
- **Mitigation**:
  - Email + SMS OTP for Phase 1 + Phase 2
  - Submit Nafath integration request early (Week 5 of Phase 1)
  - Isolate Nafath behind feature flag — portal ships without
- **Status**: Mitigated by phasing

### DEP-02 — ZATCA production CSID delay

- **Likelihood**: Medium
- **Impact**: High (blocks production go-live for invoices)
- **Priority**: High
- **Mitigation**:
  - Sandbox CSID covers Phase 1 + Phase 2 dev
  - Submit production review by Week 4
  - If production not ready by Week 8: ship Phase 2 with sandbox + clear UAT message
- **Status**: Monitor; revisit weekly

### TECH-03 — Replacement saga has unrecoverable failure mode

- **Likelihood**: Low (with careful design)
- **Impact**: Very High (data inconsistency between Tajeer + local + customer view)
- **Priority**: High
- **Mitigation**:
  - Saga state persisted at each step (`SagaInstance` table)
  - Resume-after-restart capability
  - Explicit compensation rules per step (Spec 02 §6.5)
  - Operator dashboard surfaces stuck sagas
  - Phase 2 consideration: move to Azure Durable Functions
- **Status**: Deferred to Phase 2 — Phase 1 uses manual close + new lease

### TECH-04 — Tajeer 12-hour expiry traps unfinished saves

- **Likelihood**: High (any unfinished issuance)
- **Impact**: Low (just need to recreate)
- **Priority**: Medium
- **Mitigation**:
  - Scheduled job detects + marks `EXPIRED_DRAFT`, releases vehicle reservation
  - Reminder SMS at T-2h to renter
  - UI countdown timer next to pending leases
- **Status**: Mitigated by design (Spec 02 §9.1)

### SEC-01 — PII exposure (Iqama, license numbers) in logs

- **Likelihood**: Medium (any logging bug)
- **Impact**: Very High (KSA PDPL violation)
- **Priority**: Very High
- **Mitigation**:
  - SQL Server Always Encrypted on Person.IdNumber, Driver.DriverLicenseNumber, IBAN
  - PII masking in all adapter loggers (PiiMasking class in Adapters.Common)
  - Append-only audit log for any access to sensitive entities
  - Quarterly security review
- **Status**: Mitigated by design (Spec 01 §3.1)

### DEP-03 — Tajeer production credentials review delay

- **Likelihood**: Medium
- **Impact**: High (can't go to production)
- **Priority**: High
- **Mitigation**:
  - Submit production review immediately after Phase 1 staging UAT
  - Phase 2 work continues on staging
  - Clear UAT signoff package to accelerate Elm review
- **Status**: Monitor

### TECH-05 — Local dev SQL Edge container instability

- **Likelihood**: Medium
- **Impact**: Low
- **Priority**: Low
- **Mitigation**:
  - Document Docker Desktop version requirements
  - Volume mount preserves data across restarts
  - `pnpm infra:reset` for clean slate
- **Status**: Accept; document workarounds

---

## Closed risks

(Add risks here as they're resolved or no longer applicable.)

---

## Risk update process

- **Friday review**: each active risk gets re-scored if anything changed; new risks added; closed risks moved to closed section
- **Post-incident**: every production incident or significant test failure spawns a risk entry (so we don't forget the lesson)
- **Phase boundaries**: full risk register review at start of every phase
