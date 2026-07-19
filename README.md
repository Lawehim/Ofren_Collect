# Ofren Collect

**Recurring billing + automatic reconciliation on Monnify, for small Nigerian businesses.**

Give every customer their own dedicated bank account. When money lands, Ofren Collect
verifies it with Monnify and reconciles it to the right customer, invoice, and business —
automatically, in real time, with no manual matching. The dashboard flips a payment to
**Paid** the instant the webhook is processed.

> Built for the APIConf Lagos 2026 × Monnify Developer Challenge.

---

## The problem

A small SaaS / gym / tutoring business with 30–80 recurring customers tracks payments in a
spreadsheet and chases people by hand. Two customers on the same ₦5,000 plan are impossible
to tell apart from a bank statement. Ofren Collect replaces that spreadsheet: a
**reserved-account-per-subscription** model makes every inflow self-identifying, so
reconciliation is zero-touch and always correct about the money.

## How it works (core flow)

```
Plan → enrol customer → provision Monnify reserved account → first invoice
Customer pays into the account → Monnify webhook → verify signature → verify txn server-side
→ reconcile to the owning subscription/invoice → update status → push live to the dashboard
```

Everything is **multi-tenant**: each business signs in with JWT auth and, by construction,
can only ever see its own data (TenantId on every row, an EF Core global query filter, and
per-tenant SignalR groups). The webhook is the one tenant-agnostic entry point and resolves
its tenant from the reserved account it was paid into.

## Architecture

Clean/onion architecture, dependencies pointing inward. See
[`docs/decisions/`](docs/decisions/) for architecture decisions and the PDF design docs for
the full specification.

```
src/
  OfrenCollect.SharedKernel     base building blocks (Entity, ValueObject, Money, Result)
  OfrenCollect.Domain           entities, enums, domain rules — depends only on SharedKernel
  OfrenCollect.Application      CQRS use cases (MediatR), DTOs, all interfaces, validators
  OfrenCollect.Repository       EF Core persistence: DbContext, tenant query filter, migrations
  OfrenCollect.Infrastructure   external integrations: Monnify, JWT, AI, background jobs
  OfrenCollect.Api              controllers, SignalR hub, middleware, composition root
tests/
  OfrenCollect.Domain.UnitTests
  OfrenCollect.Application.UnitTests
  OfrenCollect.Api.IntegrationTests   full pipeline + cross-tenant isolation (Testcontainers)
web/                             React SPA (Vite + TypeScript) — added in a later milestone
```

**Stack:** .NET 10 · ASP.NET Core + SignalR · EF Core + **PostgreSQL** · React (Vite + TS) ·
Monnify sandbox. All dependencies are free/OSS (see the note below).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned via `global.json`)
- [Docker](https://www.docker.com/) (for PostgreSQL locally and for integration tests)
- [Node.js](https://nodejs.org/) 20+ (for the React app, later milestone)
- A Monnify **sandbox** account (API key, secret key, contract code)

## Setup

> **Status: core backend complete and runnable.** Auth, plans, customers, enrolment,
> the webhook→verify→reconcile pipeline, the live dashboard, tenant isolation, and the
> Monnify integration are built and tested (unit + real-Postgres integration). The React
> SPA and the operational tier (audit, rate limiting, resilience) land next.

### 1. Clone and restore

```bash
git clone <repo-url>
cd "Ofren Collect"
dotnet build OfrenCollect.slnx
```

### 2. Configure secrets (never commit these — CLAUDE.md §9)

Secrets go into **.NET user-secrets**, not into any tracked file. `.env.example` documents
the shape. Example:

```bash
cd src/OfrenCollect.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OfrenDb" "Host=localhost;Port=5432;Database=ofren_collect;Username=ofren;Password=<your-password>"
dotnet user-secrets set "Jwt:SigningKey" "<a-long-random-secret>"
dotnet user-secrets set "Monnify:ApiKey" "<sandbox-api-key>"
dotnet user-secrets set "Monnify:SecretKey" "<sandbox-secret-key>"
dotnet user-secrets set "Monnify:ContractCode" "<sandbox-contract-code>"
```

Order the steps so the values line up:

```bash
docker run --name ofren-db -e POSTGRES_USER=ofren -e POSTGRES_PASSWORD=<your-password> \
  -e POSTGRES_DB=ofren_collect -p 5432:5432 -d postgres:17
```

Then point the connection string at it (use the same `<your-password>` and port). Monnify
keys are only needed for live enrolment; everything else runs without them.

### 3. Run the API

```bash
dotnet run --project src/OfrenCollect.Api
# health check:  GET http://localhost:5080/health  ->  { "status": "ok" }
```

On first run it applies the EF migrations and seeds demo data, so the app is never empty.

### 4. Try it

Log in with the seeded owner and open the dashboard:

```bash
BASE=http://localhost:5080
TOKEN=$(curl -s -X POST $BASE/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"ada@brightpath.ng","password":"password123"}' \
  | grep -o '"token":"[^"]*"' | sed 's/"token":"//;s/"$//')
curl -s $BASE/api/dashboard -H "Authorization: Bearer $TOKEN"
```

You should see the seeded subscription (Chidi Eze / Premium / reserved account 7080124933).
Register a second business via `POST /api/auth/register` and confirm its dashboard is empty —
tenant isolation in action.

## Tests

```bash
dotnet test OfrenCollect.slnx
```

Unit tests are fast and I/O-free. Integration tests spin up real PostgreSQL via
Testcontainers (**Docker must be running**) and exercise the full MediatR pipeline, auth,
and explicit cross-tenant isolation.

## Dependency & licence notes

Everything is free / open-source. Two pins are deliberate because upstream moved newer
majors to commercial licences — both are held at their last free release:

- **MediatR `12.5.0`** — last Apache-2.0 release before v13 went commercial.
- **FluentAssertions `7.2.2`** — last Apache-2.0 release before v8 moved to the Xceed licence.

All versions are pinned centrally in [`Directory.Packages.props`](Directory.Packages.props).

## The engineering contract

Development is governed by [`CLAUDE.md`](CLAUDE.md): TDD (red-green-refactor, ≥90% coverage),
CQRS via MediatR, premortem-before-done, decimal money, UTC internally, tenant isolation as a
security boundary, and secrets never in the repo.
