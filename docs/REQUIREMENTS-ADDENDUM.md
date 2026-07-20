# Requirements Addendum — Monnify capability expansion

> **Status:** Draft for review. Governs new work only; the base `Ofren_REQUIREMENTS`,
> `Ofren_USER_STORIES`, and `Ofren_TEST_CASES` remain authoritative for existing features.
> Nothing here is implemented yet. **No Monnify API shapes below are confirmed** — every
> endpoint/field marked _(verify)_ MUST be checked against the live sandbox and recorded in
> [`integrations/monnify-sandbox-notes.md`](integrations/monnify-sandbox-notes.md) before any
> code is written (CLAUDE.md §14).

## Purpose

Extend Ofren Collect beyond collection + reconciliation into three adjacent Monnify
capabilities, chosen for product fit and money-safety:

1. **Settlement reconciliation** (FR-10.x) — know when collected funds actually reach the
   business's bank, net of fees.
2. **Refunds** (FR-11.x) — return money to a customer (overpayment, cancellation), tracked to
   completion.
3. **Direct-debit Mandates** (FR-9.x) — auto-debit a customer on schedule instead of relying on
   manual pay-in; the recurring-billing upgrade.

**Recommended sequencing (lowest risk first):** Settlement → Refund → Mandates. Each ships
behind its own **feature flag** (CLAUDE.md §6) and stays dark until complete.

## Principles that bind every item here

- **Money correctness first** (§1). Decimal `Money` with currency; never bare decimals.
- **Tenant isolation is a boundary** (§8). Every new tenant-owned entity carries `TenantId`,
  derived from the token or resolved server-side — never from client input or a webhook body.
- **Idempotency in the data model** (§10). Each externally-triggered write has a unique natural
  key (Monnify reference) plus an application pre-check; safe to run twice.
- **Verify before acting** (§8, §10). Webhooks are acknowledged fast, persisted durably (inbox),
  then re-verified server-side by a drainer before any money decision.
- **Small, focused interfaces** (§5-I). New boundaries, not a fatter `IMonnifyClient`:
  `IMonnifySettlementClient`, `IMonnifyRefundClient`, `IMonnifyMandateClient` — each behind Polly
  (§10) and wired only in `Program.cs`.
- **TDD + premortem + ≥90% coverage** (§3, §4). Each FR lands red-first with unit + integration
  tests, including explicit cross-tenant isolation tests where the entity is tenant-owned.

---

## ⚠️ Open design decisions (must be answered before coding)

These are genuine architecture/tenancy questions a wrong guess makes expensive (§15).

1. **Single Monnify contract vs per-tenant sub-accounts.** Today all tenants share one
   `Monnify:ContractCode`; tenants are distinguished only by reserved account. **Settlement and
   disbursement happen at the merchant/contract level**, so a settlement batch may aggregate
   funds across tenants and cannot be cleanly attributed to one — the same class of problem as
   the tenant-less unmatched-payment count we just removed. Options: (a) treat settlement as
   **operator/platform-level** (not per-tenant); (b) move to **per-tenant Monnify sub-accounts**
   so settlement is naturally scoped. This decision gates FR-10's tenancy model.
2. **Sandbox support for direct debit / mandates.** Monnify's direct-debit product and its
   sandbox availability must be confirmed before FR-9 is scheduled; if the sandbox can't
   exercise it, FR-9 cannot meet the "verify against sandbox" bar.
3. **Refund authorisation policy.** Who may refund (Owner only?), any per-refund or daily cap,
   and whether a refund after settlement is permitted.
4. **PII handling for mandates.** Mandates need customer bank details + consent — sensitive data
   under §9 (redact in logs/audit; store only what's necessary).

---

## FR-10 · Settlement reconciliation  _(flag: `Settlement:Enabled`)_

**Why:** "Collected" (money into reserved accounts) is not the same as "settled" (money in the
business's bank, net of Monnify fees). A collections product should show both and let the owner
reconcile the gap.

### User stories
- **US-10.1** As a business owner, I want to see how much of what I've collected has actually
  been settled to my bank, so I know my real available balance.
- **US-10.2** As a business owner, I want each settlement broken down (gross, fee, net, the
  transactions it covers) so I can reconcile my bank statement.

### Functional requirements
- **FR-10.1** Enable and receive the **Settlement** webhook type; acknowledge fast, persist to the
  durable inbox, never trust the body _(verify event name + payload)_.
- **FR-10.2** A drainer re-verifies each settlement server-side before recording it _(verify
  get-settlement endpoint)_.
- **FR-10.3** Record a `SettlementEvent` — settlement reference (unique → idempotency key), gross
  `Money`, fee `Money`, net `Money`, settled-at (UTC), and the covered transaction references.
- **FR-10.4** Link covered transactions to the settlement so each `PaymentEvent` can report
  settled vs pending.
- **FR-10.5** Surface a **settled vs collected** figure on the dashboard and via the assistant
  (grounded, read-only) — scoped per the decision in Open-Question #1.
- **FR-10.6** Fees are captured as `Money`; gross = fee + net must hold (guard/validate).

### Premortem (fix each before "done")
- **Duplicate/redelivered** settlement webhook → unique settlement reference + pre-check make it a
  no-op the second time.
- **Out-of-order / partial** settlement → status modelled explicitly; a later correction updates,
  never double-counts.
- **Fee rounding / currency** → decimal only; assert gross = fee + net to the kobo.
- **Tenancy** → per Open-Question #1; until resolved, settlement is recorded platform-level and
  **not** exposed per-tenant (no cross-tenant figure, per the unmatched-payment precedent).
- **Failure** → verify fails/garbage → leave in inbox, retry; never record an unverified
  settlement.

### Tests
Unit: settlement math (gross/fee/net), idempotency pre-check, status transitions. Integration:
webhook→inbox→drainer→record on real Postgres; duplicate reference is a no-op; **cross-tenant
isolation** if/when settlement becomes tenant-owned.

---

## FR-11 · Refunds  _(flag: `Refunds:Enabled`)_

**Why:** Overpayments and cancellations need money returned to the customer, tracked to
completion and fully audited.

### User stories
- **US-11.1** As a business owner, I want to refund a customer (full or partial) against an
  original payment, so I can correct overpayments and handle cancellations.
- **US-11.2** As a business owner, I want to see a refund's status update to _Completed_ when
  Monnify confirms it, so I'm not guessing.

### Functional requirements
- **FR-11.1** Initiate a refund against an **original verified transaction**, `Owner`-only
  (§8 least privilege), amount ≤ the transaction's amount minus prior refunds _(verify refund
  endpoint + fields)_.
- **FR-11.2** Persist a `Refund` — own id, original transaction reference, requested `Money`,
  status (`Requested`/`Completed`/`Failed`), timestamps, `TenantId` (resolved from the original
  transaction, which is tenant-owned → clean scoping).
- **FR-11.3** Idempotency: a client refund key + the Monnify refund reference prevent a
  double-refund on retry/double-click.
- **FR-11.4** Receive the **Refund-completion** webhook; verify server-side; move status to
  `Completed`/`Failed` _(verify event + payload)_.
- **FR-11.5** Every refund action is **audit-logged** (§8) with actor, amount, original ref
  (account numbers redacted per §9).

### Premortem
- **Refund > original** or **cumulative refunds > original** → validate against original amount
  minus settled refunds; reject.
- **Double refund** (retry/redeliver) → refund key + reference idempotency.
- **Refund of an unmatched/tenant-less payment** → disallow (no owning tenant to authorise).
- **Refund after settlement** → per Open-Question #3.
- **Concurrency** two owners refunding the same txn → row-level guard + unique key.
- **Auth** non-Owner or wrong tenant → 403; tenant from token, original txn must belong to it.
- **Failure mid-flight** → status stays `Requested`; webhook/verify reconciles the true outcome.

### Tests
Unit: amount validation (partial, cumulative, over-refund), authorisation, idempotency. Integration:
initiate→webhook→complete on real Postgres; **Tenant A cannot refund Tenant B's transaction**;
duplicate refund key is a no-op.

---

## FR-9 · Direct-debit Mandates  _(flag: `Mandates:Enabled`)_

**Why:** Today customers must remember to pay into their reserved account. A mandate authorises
Ofren to **auto-debit** the customer's bank account when an invoice falls due — true hands-off
recurring billing. Highest value, highest complexity; schedule only after Open-Questions #2 and
#4 are resolved.

### User stories
- **US-9.1** As a business owner, I want a customer to authorise recurring debits once, so I stop
  chasing payments.
- **US-9.2** As a business owner, I want each due invoice auto-debited and reconciled exactly like
  a manual payment, so nothing changes downstream.
- **US-9.3** As a customer, I want to revoke my mandate at any time, so I stay in control.

### Functional requirements
- **FR-9.1** Create a mandate for a subscription with captured **customer consent** and the
  minimum bank details needed _(verify mandate-creation endpoint + required fields)_; store
  sensitive fields per §9 (never logged in full).
- **FR-9.2** Model mandate lifecycle: `Pending` → `Active` → `Revoked`/`Expired`; transitions
  driven by the **Mandate** webhook, verified server-side _(verify events)_.
- **FR-9.3** On an invoice due date, a background job triggers a debit for `Active` mandates
  _(verify debit endpoint)_; the debit is **idempotent per (invoice, attempt)** so a re-run never
  double-charges.
- **FR-9.4** A completed debit reconciles through the **existing** verify→reconcile→notify path —
  no parallel money logic.
- **FR-9.5** Failed debit (insufficient funds, revoked) → recorded, invoice left due, dunning/retry
  policy applied (retry schedule from config, not hard-coded).
- **FR-9.6** Revoking a mandate stops future debits immediately.

### Premortem
- **Double debit** (job retry, redelivered webhook, two schedulers) → idempotency key per invoice
  attempt + unique constraint; a debit and a **manual pay-in for the same invoice** must not both
  apply (reconciliation already guards the invoice; add a debit-vs-manual race test).
- **Wrong amount** → debit exactly the invoice's outstanding `Money`; never a hard-coded figure.
- **Debit after cancellation/revocation** → check mandate + subscription status at trigger time.
- **Consent/PII** → consent recorded and auditable; bank details redacted in logs/audit (§9).
- **Tenancy** → mandate is per-subscription → per-tenant; carries `TenantId`; isolation test.
- **Failure/partial** → debit initiated but process dies → status `Pending`, reconciled by
  verify/webhook; never assume success.
- **Auth** → only the owning tenant can create/revoke; tenant from token.

### Tests
Unit: due-date trigger selection, idempotency per attempt, amount = outstanding, lifecycle
transitions, revoke stops debits. Integration: create→active→due→debit→reconcile on real Postgres;
duplicate attempt is a no-op; debit + manual pay-in don't double-apply; **cross-tenant isolation**.

---

## Cross-cutting delivery notes

- **Monnify dashboard:** enable the additional webhook types (Settlement, Refund completion,
  Mandate) alongside the existing Transaction-completion; all target the durable inbox.
- **Config (§6):** `Settlement:Enabled`, `Refunds:Enabled`, `Mandates:Enabled`, plus any retry
  schedules — all `IOptions<T>`, no inline values.
- **Resilience (§10):** every new outbound Monnify call through Polly (retry + timeout + breaker).
- **Migrations:** new tables (`SettlementEvent`, `Refund`, `Mandate`, mandate debit attempts) via
  EF Core; tenant-owned tables get the global query filter + `TenantId` stamping.
- **Docs (§14):** update `ARCHITECTURE_DESIGN` and `monnify-sandbox-notes.md` as shapes are
  confirmed; keep this addendum in sync.

## Proposed next step

1. You resolve Open-Questions #1–#4 (especially the single-contract vs sub-account model — it
   changes FR-10's whole tenancy design).
2. I verify the Settlement (then Refund, then Mandate) API shapes against the sandbox and record
   them.
3. We implement **Settlement first**, TDD, behind its flag, and only then move to Refund and
   Mandates.
