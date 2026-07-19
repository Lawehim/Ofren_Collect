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

## How it works

```
Plan → enrol customer → provision Monnify reserved account → first invoice
Customer pays into the account → Monnify webhook → verify signature → persist durably (200)
→ background drainer re-verifies the txn server-side → reconcile to the owning invoice
→ update status → push live to the dashboard over SignalR
```

Everything is **multi-tenant**: each business signs in with JWT auth and, by construction,
can only ever see its own data (a `TenantId` on every row, an EF Core global query filter, and
per-tenant SignalR groups). The webhook is the one tenant-agnostic entry point and resolves
its tenant from the reserved account it was paid into. Every API call is **audit-logged**
(sanitised), the API is **rate-limited**, and Monnify calls are wrapped in **retry + circuit
breaker**.

## Architecture

Clean/onion architecture, dependencies pointing inward. See
[`docs/decisions/`](docs/decisions/) for architecture decisions and the PDF design docs for
the full specification.

```
src/
  OfrenCollect.SharedKernel     Money (kobo-precise), Entity/AggregateRoot, Guard
  OfrenCollect.Domain           entities + reconciliation rules — depends only on SharedKernel
  OfrenCollect.Application      CQRS use cases (MediatR), DTOs, interfaces, validators, behaviors
  OfrenCollect.Repository       EF Core: DbContext, global tenant filter, repositories, migrations
  OfrenCollect.Infrastructure   Monnify client, JWT, password hashing, audit, resilience, jobs
  OfrenCollect.Api              controllers, SignalR hub, middleware, rate limiter, composition root
tests/
  OfrenCollect.Domain.UnitTests
  OfrenCollect.Application.UnitTests
  OfrenCollect.Infrastructure.UnitTests
  OfrenCollect.Api.IntegrationTests   full pipeline + tenant isolation on real Postgres (Testcontainers)
web/                            React SPA (Vite + TypeScript) — the live dashboard
```

**Stack:** .NET 10 · ASP.NET Core + SignalR · EF Core + **PostgreSQL** · React (Vite + TS) ·
Monnify sandbox. All dependencies are free / open-source.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned via `global.json`)
- [Docker](https://www.docker.com/) — for PostgreSQL and the integration tests
- [Node.js](https://nodejs.org/) 20+ — for the React app
- *(optional)* A Monnify **sandbox** account (API key, secret key, contract code) — only needed
  for live enrolment; everything else runs without it.

> **Windows note:** if `git clone` reports *"Filename too long"*, enable long paths once with
> `git config --global core.longpaths true`, then clone (or clone into a short path like
> `C:\src\ofren`).

## Run it (from a clean clone)

### 1. Start PostgreSQL

```bash
docker run --name ofren-db -e POSTGRES_USER=ofren -e POSTGRES_PASSWORD=ofrendev \
  -e POSTGRES_DB=ofren_collect -p 5432:5432 -d postgres:17
```

### 2. Configure secrets (never committed — they live in .NET user-secrets)

```bash
cd src/OfrenCollect.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OfrenDb" "Host=localhost;Port=5432;Database=ofren_collect;Username=ofren;Password=ofrendev"
dotnet user-secrets set "Jwt:SigningKey" "a-long-random-development-signing-key-change-me"
# Optional — only for live enrolment against Monnify sandbox:
# dotnet user-secrets set "Monnify:ApiKey" "<sandbox-api-key>"
# dotnet user-secrets set "Monnify:SecretKey" "<sandbox-secret-key>"
# dotnet user-secrets set "Monnify:ContractCode" "<sandbox-contract-code>"
cd ../..
```

`.env.example` documents the full shape of the configuration.

### 3. Run the API

```bash
dotnet run --project src/OfrenCollect.Api
```

It applies the EF migrations and seeds demo data on first run (so the app is never empty), and
serves on **http://localhost:5080** — check `http://localhost:5080/health`.

### 4. Run the web app

```bash
cd web
npm install
npm run dev
```

Open **http://localhost:5173** and log in with the seeded owner:

> **ada@brightpath.ng** / **password123**

You'll see the live dashboard: subscriptions, colour-coded statuses, and a summary strip.
Create a plan, register a customer, and — with Monnify keys configured — enrol them to
provision a real reserved account. Register a second business to see tenant isolation: its
dashboard is empty.

### Quick API check (no browser)

```bash
BASE=http://localhost:5080
TOKEN=$(curl -s -X POST $BASE/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"ada@brightpath.ng","password":"password123"}' \
  | grep -o '"token":"[^"]*"' | sed 's/"token":"//;s/"$//')
curl -s $BASE/api/dashboard -H "Authorization: Bearer $TOKEN"
```

## Live reconciliation (Monnify sandbox)

With Monnify sandbox keys set (step 2), enrolment provisions a real reserved account. To see a
payment reconcile:

1. Keep the dashboard open (it holds a live SignalR connection).
2. Enrol a customer to get a reserved account number.
3. Pay into it via Monnify's Banking App Simulator (`https://websim.sdk.monnify.com/#/bankingapp`
   → Transfer → the reserved account's bank + number + the plan amount).
4. Monnify calls the webhook (expose it with a dev tunnel), or replay it locally; the backend
   re-verifies the transaction and the invoice flips to **Paid** live.

See [`docs/integrations/monnify-sandbox-notes.md`](docs/integrations/monnify-sandbox-notes.md)
for the confirmed sandbox contracts and the webhook signature scheme.

## Tests

```bash
dotnet test OfrenCollect.slnx     # backend: unit + integration (Docker must be running)
cd web && npm run build           # frontend: type-check + build
```

Unit tests are fast and I/O-free. Integration tests spin up **real PostgreSQL** via
Testcontainers and exercise the full MediatR pipeline, auth, reconciliation, and explicit
cross-tenant isolation.

## What's built

- **Auth & multi-tenancy** — JWT register/login, PBKDF2 hashing, tenant-from-token, EF global
  query filter + anti-forge stamping, per-tenant SignalR.
- **Core** — plans, customers, enrolment (reserved account + first invoice), the reconciliation
  engine (idempotent, verify-before-act), the live dashboard.
- **Monnify** — reserved-account creation, transaction verification, and HMAC-SHA512 webhook
  signature verification, behind one `IMonnifyClient`.
- **Operational** — audit logging (sanitised, Owner-queryable), rate limiting (per-tenant +
  per-IP), Polly retry/circuit-breaker, and a durable webhook inbox with a background drainer.

## Dependency & licence notes

Everything is free / open-source. Two pins are deliberate because upstream moved newer majors to
commercial licences — both are held at their last free release:

- **MediatR `12.5.0`** — last Apache-2.0 release before v13 went commercial.
- **FluentAssertions `7.2.2`** — last Apache-2.0 release before v8 moved to the Xceed licence.

All versions are pinned centrally in [`Directory.Packages.props`](Directory.Packages.props).

## The engineering contract

Development is governed by [`CLAUDE.md`](CLAUDE.md): TDD (red-green-refactor), CQRS via MediatR,
premortem-before-done, decimal money, UTC internally, tenant isolation as a security boundary,
and secrets never in the repo.
