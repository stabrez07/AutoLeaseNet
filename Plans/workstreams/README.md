# Workstream Plans

Per-workstream **task-level plans** following the [superpowers](https://github.com/obra/superpowers) methodology: each plan breaks a workstream into precisely-scoped 2-5 minute tasks before any coding starts.

## Convention

```
workstreams/
├── README.md                                (this file)
├── 2026-05-20-tajeer-save-contract/         (slug = ISO date + short title)
│   ├── plan.md                              (the task plan)
│   ├── notes.md                             (running notes during execution)
│   └── retrospective.md                     (lessons learned, written at workstream close)
├── 2026-05-27-e-check-sketch-component/
│   └── plan.md
└── ...
```

## Plan template

Every `plan.md` must contain:

1. **Goal** — one sentence; what "done" looks like
2. **Scope** — what's in and out
3. **Tasks** — atomic 2-5 min steps; checkbox per task
4. **Verification** — how each task is verified done (test passes, command output, manual check)
5. **Dependencies** — other workstreams, adapters, or credentials this needs
6. **Risks** — what could derail this and the mitigation

## When to create a workstream

- Start of any work that takes more than 1 day
- Anything spanning multiple commits / PRs
- Anything touching more than 2 packages

## When to skip

- Trivial single-file changes
- Bug fixes with a single failing test → fix → green cycle
- Documentation-only changes (just commit)

## Workstream lifecycle

1. **Plan**: write `plan.md` before any code
2. **Execute**: check off tasks as you go; capture decisions in `notes.md`
3. **Review**: get a second opinion (Claude subagent or self after a break)
4. **Verify**: run all verification steps; CI green
5. **Close**: write `retrospective.md` — what went well, what to change next time
6. **Archive**: keep folder for reference; never delete

## Phase 1 workstreams (planned)

These will get folders here as each is initiated:

- `week-1-foundation-tajeer-happy-path`
- `week-2-customer-vehicle-driver-uis`
- `week-2-save-contract-form`
- `week-3-echeck-sketch-component`
- `week-3-checkout-checkin-saga`
- `week-3-close-extend-suspend`
- `week-4-quotation-aggregate`
- `week-4-approval-workflow-engine`
- `week-4-zatca-invoice-generation`
- `week-4-demo-prep`
