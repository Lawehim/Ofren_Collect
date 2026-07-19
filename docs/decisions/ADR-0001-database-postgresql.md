# ADR-0001 — PostgreSQL is the datastore (supersedes SQL Server references)

- **Status:** Accepted
- **Date:** 2026-07-19
- **Deciders:** Product owner + engineering
- **Supersedes:** SQL Server Express / LocalDB references in the PDF documents
  (`Ofren_ARCHITECTURE_DESIGN`, `Ofren_REQUIREMENTS`, `Ofren_TEST_CASES`).

## Context

`CLAUDE.md` — the binding engineering contract — mandates **PostgreSQL** (§2.2:
"EF Core (Npgsql/PostgreSQL provider)") and requires integration tests to run against
**real PostgreSQL via Testcontainers** (§4.3: "test what you ship; production is Postgres").

The earlier planning PDFs (architecture, requirements NFR-4.1, test cases TC-8.2) instead
specify SQL Server Express / LocalDB. These two positions are mutually exclusive and must
be reconciled deliberately, not left to drift (`CLAUDE.md` §1, §14).

## Decision

**PostgreSQL is authoritative.** The SQL Server / LocalDB references in the PDF documents
are considered superseded by this ADR and by `CLAUDE.md`.

Rationale:

1. **`CLAUDE.md` is the newest, binding artifact** and explicitly names Npgsql/PostgreSQL.
2. **Truly free and open-source.** PostgreSQL (PostgreSQL Licence, BSD-style) fits §2.2's
   "free and open-source (MIT/Apache/BSD or similar)" spirit more cleanly than the
   proprietary-though-free SQL Server Express.
3. **"Test what you ship."** Testcontainers gives us real-Postgres integration tests
   without a provider mismatch between test and production.

## Consequences

- EF Core uses `Npgsql.EntityFrameworkCore.PostgreSQL`; connection strings target Postgres.
- Local dev and integration tests run Postgres via Docker (Testcontainers for tests). The
  Docker daemon must be running for the integration-test suite.
- The PDFs are not edited (binary); this ADR is the reconciliation of record and the README
  documents the Postgres setup. If the PDFs are ever regenerated, they should be updated to
  match.
- No functional requirement changes — only the datastore engine. All FR/NFR/TC IDs still
  apply; wherever they say "SQL Server / LocalDB", read "PostgreSQL / Docker".
