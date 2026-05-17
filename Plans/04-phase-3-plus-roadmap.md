# 04 — Phase 3+ Roadmap: Telematics, AI, Mobile, Multi-country

**Status**: 📋 Draft — refined as Phase 1/2 land
**Phase**: 3 (Weeks 9+) and beyond
**Goal**: Productionize, intelligent features, mobile, regional expansion

---

## Phase 3 — Intelligence + Mobile (Weeks 9-16)

### Telematics & Wasl (Weeks 9-10)

- Integrate Mix Telematics OR Geotab (vendor decision pending fleet size + cost analysis)
- Build vendor-agnostic abstraction at `IT­elematicsProvider` port
- Real-time vehicle location, trip history, harsh-event detection (braking, speeding, idling)
- Wasl integration for KSA TGA mandatory fleet tracking
- Geofencing engine (per-customer geofences, alerts on exit/entry)
- Telemetry-driven odometer updates (replaces self-reported)
- Vehicle health score derived from DTCs (diagnostic trouble codes)

### Nafath B2C login (Week 11)

- Federate Entra External ID with Nafath OIDC
- Update Customer Portal to offer Nafath as primary, email+SMS OTP as fallback
- KYC backfill: when existing customers log in via Nafath, verify Iqama/NID matches stored values

### Payment gateways (Weeks 11-12)

- Adapter pattern: `IPaymentGateway` with implementations for HyperPay, Moyasar, PayTabs
- Customer Portal: pay invoice via card/Mada/STC Pay/Apple Pay
- Auto-debit setup for B2B customers (recurring billing)
- Dunning workflow for overdue invoices

### MOI / Absher fines integration (Weeks 12-13)

- Periodic fetch of traffic violations against fleet vehicles
- Pass-through invoicing to customer (with admin fee per policy)
- Driver-level fine attribution where evidence supports

### AI features (Weeks 13-15)

- **AI Copilot inside Customer Portal**: natural language queries ("show vehicles overdue for service in Riyadh") via Azure OpenAI tool-calling against BFF APIs
- **Document AI on E-Check photos**: Azure AI Vision custom model for damage detection — assist, not authority
- **OCR on Iqama/license**: Azure Document Intelligence for driver onboarding form auto-fill
- **Driver scoring + gamification**: harsh-event-derived score, per-driver dashboard, corporate roll-up
- **Predictive maintenance**: replace static mileage rules with telematics-derived service triggers

### Mobile app (Weeks 14-16)

- React Native OR .NET MAUI (decision pending: team familiarity, OEM library support)
- Driver-first: assigned vehicle, report incident, request service, see PMS reminders
- Ops-friendly: E-Check with offline-first sync (SQLite + conflict resolution)
- Push notifications via Azure Notification Hubs

### Car Servicing App integration (Week 16)

- `ICarServicingClient` adapter to internal workshop system
- Service booking sync; workshop status updates back to lease
- Pre/post-service inspection sharing

---

## Phase 4 — UAE expansion (TBD)

When market signals justify (likely 6-12 months after Phase 1 launch):

- TAMM (Abu Dhabi gov services)
- UAE Pass (federated identity)
- Salik / Darb tolls
- Mulkiya (UAE vehicle registration)
- Muroor fines
- FTA e-invoicing (UAE Phase 2 equivalent)
- Multi-currency (AED + SAR)
- Multi-language tax invoices

Architectural prep: Branch.CountryCode, Lookup.CountryScope already modeled in [Spec 01](../Specs/01-multi-tenancy-and-domain-model.md) for this.

---

## Phase 5 — Other GCC (TBD)

Kuwait, Bahrain, Oman, Qatar — each has its own regulators. Approach: copy UAE pattern; per-country adapter packages.

---

## Cross-phase recurring concerns

- **Quarterly security review**: dependency scan (`dotnet list package --vulnerable`), pen-test, RLS validation
- **Annual ZATCA EGS renewal**: production CSID expires every 12 months
- **Annual Tajeer integration test cycle**: spec versions evolve; contract snapshot tests catch breaking changes
- **D365 upgrades**: F&O receives quarterly updates; coordinate with D365 team for API changes
- **Documentation freshness**: Specs reviewed every quarter; deprecated decisions moved to ADR archive

---

## Decision points to revisit each phase

| Decision | Revisit when |
|---|---|
| Monolith vs microservices | When team grows beyond 3 devs OR load patterns demand it |
| Single-tenant vs SaaS multi-tenant | When 2nd leasing company customer signs |
| In-house vs vendor BPM (workflow engine) | If approval workflows become a deep configuration product |
| SQL Server vs PostgreSQL | If Azure SQL costs become a problem at scale |
| Build vs buy CRM | If we outgrow D365 CRM integration |
| OEM telematics vs aftermarket | When fleet composition shifts |
