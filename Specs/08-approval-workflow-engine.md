# 08 — Approval Workflow Engine

**Status**: ⏳ Placeholder — to be expanded just-in-time before Week 4 of Phase 1 (when quotation work begins)
**Phase**: Foundation
**Owner**: Architecture
**Depends on**: [01](./01-multi-tenancy-and-domain-model.md), [02](./02-state-machines-and-sagas.md), [06](./06-bff-api-surface.md)
**Last updated**: 2026-05-17

---

## Status: placeholder

This doc is intentionally minimal until the quotation workstream starts. The high-level design is already covered in:

- [Doc 01 §5.4](./01-multi-tenancy-and-domain-model.md#54-sales-quotation) — `Quotation`, `QuotationLine`, `QuotationApproval`, `ApprovalTier` entities
- [Doc 02 §4.1](./02-state-machines-and-sagas.md#41-quotation) — Quotation state machine
- [Doc 02 §6.1](./02-state-machines-and-sagas.md#61-quote-approval-workflow-saga) — Quote Approval Workflow Saga (3-tier flow with sequence diagram, idempotency, edge cases)
- [Doc 06 §5.6](./06-bff-api-surface.md#56-approvals-the-workflow-engine-surface) — Approvals API endpoints (pending, decide, reassign)

## What this doc will cover when expanded

1. **Engine design** — config-driven, snapshotted at submit time, applicable to multiple resource types
2. **Config schema** — `ApprovalTier` table fields, effective dating, branch/region scoping (Phase 2)
3. **Evaluator algorithm** — given resource type + amount → ordered list of required tiers
4. **Delegation model** — role-pool vs explicit `AssignedUserId`; cover rules for absences
5. **Recall semantics** — submitter recall while no tier approved; admin override
6. **Audit trail** — append-only `ApprovalAuditLog` with snapshots
7. **Notification triggers** — email/SMS/in-app to approvers per tier
8. **UI implications** — approver inbox (`GET /approvals/pending`), quote-detail approval chain display
9. **Edge cases** — approver loses role mid-flow, no eligible approver for tier, race conditions, self-approval blocked, customer cancels during approval
10. **Extensibility** — beyond Quotation (Discount approval, Early Termination, Refund Request); `IApprovalRuleEvaluator` interface for custom criteria
11. **Anti-patterns** — never hardcode thresholds, never store approval roles in JWT (always check current DB state)

## Inputs already locked

- 3-tier tiered approval by amount (per [project decisions](../../memory/project_decisions.md))
- Lives in application layer (not an adapter — pure business logic)
- DB-config-driven; thresholds editable via admin endpoints (per [doc 06 §5.16](./06-bff-api-surface.md#516-admin-settings))

## When this doc gets expanded

Trigger: Start of Week 4 in Phase 1 (quotation workstream), OR earlier if a downstream design needs the detail.
