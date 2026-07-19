# CLAUDE.md — Engineering ground rules for Ofren Collect

This file governs how Ofren Collect is built. It is binding on every contributor —
human or AI agent. If a change violates a rule here, it is not done, regardless of
whether it "works." When a rule genuinely blocks progress, stop and raise it; do not
quietly break it.

Read this file in full before writing any code. Re-read the relevant section before
starting each task.

Companion documents: `ARCHITECTURE_DESIGN` (system design), `REQUIREMENTS`,
`USER_STORIES`, `TEST_CASES`. This file is the *how*; those are the *what*.

---

## 1. Prime directives (non-negotiable)

1. **Correctness of money before everything.** This system moves and reconciles money.
   A feature that is fast, elegant, and subtly wrong about a naira is a failure. When in
   doubt, choose the safer, more auditable path.
2. **Tenant isolation is a security boundary, not a feature.** No code path may ever let
   one tenant read, write, or observe another tenant's data. A leak here is a critical
   incident, not a bug.
3. **Test-driven, always.** No production code is written before a failing test that
   requires it. See §4.
4. **Premortem every feature before marking it done.** See §3. "It compiles" and "the
   happy path works" are not done.
5. **Nothing is hard-coded.** See §6.
6. **Secrets never touch the repo.** See §9.

If any generated code conflicts with these, the code is wrong.

---

## 2. Architecture & patterns

### 2.1 Style
- **Clean/onion architecture**, dependencies pointing inward:
  `Api → Application → Domain → SharedKernel`. `Infrastructure` and `Repository` each
  implement `Application` interfaces (Repository = EF Core persistence; Infrastructure =
  external integrations such as Monnify, JWT, AI, jobs). `SharedKernel` holds dependency-free
  building blocks (base `Entity`/`ValueObject`, `Money`, `Result`, `IClock`) and depends on
  nothing; `Domain` depends only on `SharedKernel`. Never reference `Infrastructure` or
  `Repository` types from `Application` or `Domain`; concrete wiring lives only in `Api`.
- **CQRS via MediatR** (free, MIT). Every use case is a `Command` or `Query` with its own
  handler. Controllers and hubs are thin: they build a request, send it through MediatR,
  and map the result. No business logic in controllers.
  - Commands mutate state and return only what the caller needs (id, status). Queries
    never mutate. Do not let a query cause a write.
- **Validation via FluentValidation** (free) wired as a MediatR pipeline behavior — every
  command/query is validated before its handler runs.
- **Cross-cutting concerns are pipeline behaviors**, not sprinkled into handlers:
  validation, logging/audit correlation, and transaction scope each live in a behavior.
- **Mapping**: prefer explicit hand-written mapping (a `ToDto()` or a small mapper) over
  reflection-based auto-mapping. Explicit is debuggable and has no magic.

### 2.2 Free tools only
Every dependency must be free and open-source (MIT/Apache/BSD or similar). Approved core:
MediatR, FluentValidation, EF Core (Npgsql/PostgreSQL provider), Polly, xUnit, FluentAssertions,
NSubstitute (or Moq), Testcontainers, Respawn, Serilog. Anything else: justify it in the
PR and confirm the licence before adding.

### 2.3 Boundaries that must stay behind interfaces
`IMonnifyClient`, `IAiAssistant`, `IReconciliationNotifier`, `ITenantContext`,
`ICurrentUser`, `IAuditLogger`, clock (`IClock`/`TimeProvider`), and all repositories.
No handler calls a vendor SDK, `DateTime.Now`, or `HttpClient` directly.

---

## 3. Premortem-before-done (the definition of done)

Before any feature, endpoint, or handler is marked done, the author performs a written
**premortem**: assume it has already failed in production, and list how. Then fix each
plausible failure *before* claiming done. At minimum, walk these:

- **Money**: under/over/duplicate/out-of-order payment, zero, negative, rounding,
  currency, concurrent payments to the same invoice.
- **Tenancy**: could another tenant's data leak in or out through this path? Is the tenant
  derived from the token, never the request body?
- **Idempotency**: what if this runs twice (retry, double-click, redelivered webhook)?
- **Failure**: dependency down/slow/returns garbage; partial write; process killed
  mid-operation.
- **Auth**: unauthenticated, wrong role, expired token.
- **Input**: null, empty, huge, malformed, injection, wrong type.
- **Concurrency**: two requests racing the same row.

Every plausible failure is either handled, or explicitly documented as out of scope with a
reason. A feature with an unexamined failure mode is **not done**. Record the premortem in
the PR description.

---

## 4. Testing (TDD, mandatory, ≥90%)

### 4.1 Process
- **Red → Green → Refactor.** Write a failing test first, make it pass with the simplest
  code, then refactor with tests green. No production code without a test that demanded it.
- Tests are part of the same commit as the code they cover. A PR that adds behavior without
  tests is rejected.

### 4.2 Coverage
- **Minimum 90% line and branch coverage**, enforced in CI — the build fails below it.
- Coverage is a floor, not a goal. 90% of meaningful tests, not 90% gamed with trivial
  asserts. The reconciliation engine, tenant isolation, auth, and money math target ~100%.
- Do not exclude files from coverage to hit the number. Exclusions require a written reason
  and review (generated migrations and `Program.cs` wiring are the only routine ones).

### 4.3 What must exist
- **Unit tests** for every handler, domain rule, and the reconciliation engine — fast, no
  I/O, dependencies faked (NSubstitute/Moq).
- **Integration tests** are compulsory, not optional. They run against **real PostgreSQL**
  via Testcontainers (never a different provider for tests — test what you ship; production
  is Postgres, so tests are Postgres), exercise
  the full MediatR pipeline, and cover: the webhook→verify→reconcile→notify path, auth,
  and **explicit cross-tenant isolation tests** (Tenant A must never see Tenant B).
- Reset DB state between integration tests (Respawn) so tests are independent and order-free.
- **Every bug fix starts with a failing test** that reproduces the bug, then the fix makes
  it green. No fix without a regression test.

### 4.4 Test quality
- One behavior per test. Arrange-Act-Assert. Name tests by behavior:
  `Reconcile_WhenAmountBelowDue_MarksInvoiceUnderpaid`.
- FluentAssertions for readable assertions. No logic (loops/conditionals) in tests.
- Tests must be deterministic: inject the clock, never sleep, never depend on wall-time,
  ordering, or network. A flaky test is a failing test — fix or delete it, never ignore it.

---

## 5. SOLID & code design

- **S** — one reason to change per class. Fat handlers that validate + fetch + decide +
  persist + notify get split.
- **O** — extend via new handlers/strategies, not by editing switch statements that grow
  forever.
- **L** — a substitute for an interface must honor its contract (e.g. `NullAiAssistant`
  behaves like a real one, just disabled).
- **I** — small, focused interfaces. No `IService` god-interface.
- **D** — depend on abstractions; concretes are wired only in the composition root
  (`Program.cs`).

Also:
- **DRY**, but not prematurely — duplication is cheaper than the wrong abstraction. Extract
  on the third occurrence, not the first.
- **YAGNI** — build what the requirement needs, not a framework for imagined futures.
- **Composition over inheritance.** Deep inheritance trees are banned; prefer interfaces
  and composition.
- **Fail fast** — validate at the boundary and throw/return early; no deeply nested happy
  paths.

---

## 6. No hard-coding

- **No magic numbers or strings.** Named constants, enums, or configuration. `0.05m` is a
  crime; `Fees.LateThreshold` is fine.
- **All environment-specific values come from configuration** (`IOptions<T>` bound to
  strongly-typed settings): connection strings, base URLs, Monnify contract code, JWT
  issuer/audience/lifetime, rate-limit thresholds, retry/breaker settings, the AI feature
  flag and provider. Never inline.
- **No hard-coded tenant, user, account, or test data in production code paths.** Seed data
  lives in a clearly separated seeder, run only in dev.
- **Feature flags** gate optional features (the AI assistant); flipping one is a config
  change, never a code edit.
- **No hard-coded secrets, tokens, connection strings, or keys — ever.** See §9.

---

## 7. One class per file (and file conventions)

- **One public type per file**, filename == type name (`ReconcilePaymentHandler.cs`).
  Small tightly-coupled private/nested types or a command+handler pair may share a file
  only when they are never used apart; when in doubt, split.
- Folder structure follows the architecture layers and features (vertical slices):
  `Application/Reconciliation/ReconcilePayment/{Command,Handler,Validator}.cs`.
- No `Utils`/`Helpers`/`Common` dumping grounds. A helper belongs to a named concept; name
  it for that concept.
- One project per layer — `SharedKernel`, `Domain`, `Application`, `Repository`,
  `Infrastructure`, `Api` — plus test projects mirroring them
  (`Domain.UnitTests`, `Application.UnitTests`, `Infrastructure.UnitTests`,
  `Api.IntegrationTests`).

---

## 8. Multi-tenancy & security rules (build-time)

- **The tenant comes from the authenticated token via `ITenantContext`. Never from a route,
  query, header, or body.** Any code that reads a tenant id from client input is wrong.
- **Every tenant-owned entity carries `TenantId`.** The EF Core global query filter enforces
  scoping on every read; `SaveChanges` stamps `TenantId` automatically. Do not write a query
  that bypasses the filter without an explicit, reviewed reason (and a test).
- **Authorize by default.** Endpoints require a valid token unless explicitly marked
  anonymous (only register, login, and the Monnify webhook). The webhook authenticates by
  signature and resolves tenant from the reserved account.
- **Never trust a webhook body.** Verify signature, then verify the transaction server-side
  before acting.
- **Passwords**: hashed with a strong KDF only (never reversible, never logged).
- **Least privilege**: role checks (`Owner`/`Staff`) on sensitive actions like audit access.

---

## 9. Secrets & configuration

- Secrets (Monnify keys, JWT signing key, DB credentials, AI provider key) come only from
  **user-secrets (local)** or **environment variables**. Never in `appsettings.json`,
  never in code, never in tests, never committed.
- The repo contains only `.env.example` with placeholder values, and `.gitignore` blocks
  real secret files.
- **Nothing sensitive is logged**: no full secrets, tokens, passwords, or full account
  numbers/PANs — in logs or in audit entries (redact before persistence).
- If a secret is ever committed, treat it as compromised: rotate it, don't just delete it.

---

## 10. Error handling & resilience

- **One global exception-handling middleware** returns uniform, safe JSON errors. Never leak
  stack traces, SQL, or internals to clients.
- **No swallowed exceptions.** Never `catch {}`. Catch only what you can handle; otherwise
  let it bubble to the middleware. Every catch either recovers meaningfully or rethrows with
  context.
- **All outbound Monnify/AI calls go through Polly** (retry with backoff + timeout + circuit
  breaker), configured from settings, behind the client interface.
- **Idempotency is enforced in the data model** (unique Monnify transaction reference) plus
  an application-level pre-check. Reconciliation must be safe to run twice.
- **Webhooks**: acknowledge fast (200), process durably (inbox pattern), verify before
  acting. A crash mid-processing must not lose an acknowledged payment.
- Fail loud in development, fail safe in production.

---

## 11. Async, data & performance

- **Async all the way** for I/O: `async`/`await`, `CancellationToken` threaded through every
  handler, repository, and external call. No `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`
  — they deadlock and hide errors.
- **EF Core**: async queries; `AsNoTracking()` for reads; project to DTOs in queries (no
  leaking entities out of the data layer); explicit transactions where a unit of work spans
  multiple writes (enrolment: subscription + reserved account + first invoice commit
  together or not at all).
- **No N+1 queries.** Load what you need in one round trip.
- **Decimal for money, always.** Never `float`/`double` for currency. Money carries its
  currency; no bare decimals passed around as "amounts" without context.
- **UTC everywhere** internally; convert to WAT only at the presentation edge. Time comes
  from the injected clock, never `DateTime.Now`.

---

## 12. Naming, style & readability

- Follow standard .NET conventions: `PascalCase` types/methods, `camelCase` locals,
  `_camelCase` private fields, `IPascalCase` interfaces. Async methods end in `Async`.
- Names say intent. `CalculateOutstandingBalance`, not `Calc` or `DoWork`. Booleans read as
  assertions: `isOverdue`, `hasMatchingInvoice`.
- Keep methods short and single-purpose; if you need a comment to explain a block, extract
  a well-named method instead.
- **Comments explain *why*, never *what*.** The code says what. Delete commented-out code.
- Enforce formatting with `.editorconfig` and `dotnet format` in CI. Warnings treated as
  errors on the build. Enable nullable reference types; no `!` null-forgiving without a
  justified reason.
- No dead code, no unused usings, no TODOs left in `main` (turn them into tracked issues).

---

## 13. Git, PRs & CI

- **Small, focused commits** with meaningful messages (imperative mood: "Add underpaid
  reconciliation branch"). Reference the requirement/story/test id where relevant.
- **A change is not done until CI is green**: build, all tests, ≥90% coverage, `dotnet
  format` check, and no analyzer warnings all pass.
- Every PR states: what it does, the premortem (§3), and which tests cover it.
- **`main` is always releasable.** Never merge red. Never merge to hit a deadline what you
  wouldn't merge otherwise.
- Do not disable a failing test, lower the coverage gate, or comment out an assertion to get
  a green build. Fix the cause.

---

## 14. Documentation & self-knowledge

- Public behavior and non-obvious decisions are documented in code XML docs or a short note
  in the relevant doc; keep `ARCHITECTURE_DESIGN` and this file in sync when a rule changes.
- The README's setup steps must always actually work from a clean clone — if a change breaks
  setup, the change includes the README fix.
- Never invent Monnify API shapes: verify against the live sandbox before coding, and keep
  the integration behind `IMonnifyClient` so corrections touch one file.

---

## 15. For the AI agent specifically

- **Do not claim done prematurely.** Run the tests. Run the premortem. If you cannot run
  something, say so explicitly rather than asserting it passed.
- **Do not hallucinate APIs, packages, or config keys.** If unsure of a Monnify field, a
  library method, or a setting name, verify it or flag the uncertainty — never guess and
  present it as fact.
- **Prefer the boring, proven solution.** This is fintech, not a playground.
- **When a requirement is ambiguous, ask** rather than assuming; a wrong assumption about
  money or tenancy is expensive.
- **Never weaken a rule in this file to make a task easier.** If a rule is genuinely
  blocking correct work, surface it for a human decision — do not silently bypass it.
- Respect scope and priority from `TEST_CASES` and `USER_STORIES`: build the P1 core
  correctly before reaching for stretch features.

---

*This document is the contract. Code is reviewed against it. When it and reality disagree,
fix one of them deliberately — never ignore the gap.*
