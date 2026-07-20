# Deploying Ofren Collect (free tier)

Three free services, no shared state between them:

| Piece | Service | Notes |
|-------|---------|-------|
| PostgreSQL | **Neon** | Free, always-on, serverless. |
| API (.NET 10 + SignalR) | **Render** | Free web service via Docker; public HTTPS; WebSockets. Sleeps after ~15 min idle. |
| React SPA | **Vercel** (or Netlify) | Free static hosting; points at the Render API. |

Deploy order matters because of the URLs: **Neon → Render → Vercel → (set CORS back on Render)**.

---

## 1. Database — Neon

1. Create a project at [neon.tech](https://neon.tech) (no card).
2. Copy the connection string. Use the **.NET / Npgsql** form and ensure SSL is on:

   ```
   Host=<ep-xxx>.<region>.aws.neon.tech;Database=<db>;Username=<user>;Password=<pwd>;SSL Mode=Require;Trust Server Certificate=true
   ```

   The app applies EF migrations and seeds demo data on first start, so the schema is created for you.

## 2. API — Render

**Option A — Blueprint (uses [`render.yaml`](../render.yaml)):** Render → **New → Blueprint** → pick this repo. It creates the Docker web service; then set the secret env vars below in the dashboard.

**Option B — manual:** Render → **New → Web Service** → connect the repo → **Runtime: Docker** (it finds the root `Dockerfile`) → Plan: **Free** → Health check path: `/health`.

Set these **environment variables** (dashboard → Environment):

| Key | Value |
|-----|-------|
| `ConnectionStrings__OfrenDb` | the Neon connection string from step 1 |
| `Jwt__SigningKey` | a long random secret |
| `Cors__Origin` | your SPA URL (fill after step 3, e.g. `https://ofren.vercel.app`) |
| `Monnify__ApiKey` / `Monnify__SecretKey` / `Monnify__ContractCode` | your sandbox keys |
| `Monnify__VerifyWebhookSignature` | `false` (sandbox doesn't send the signature) |

Optional — to enable the **AI assistant** (grounded, read-only) with a free hosted model:

| Key | Value |
|-----|-------|
| `Ai__Enabled` | `true` |
| `Ai__BaseUrl` | `https://api.groq.com/openai` (Groq — free key at [console.groq.com](https://console.groq.com)) |
| `Ai__Model` | `llama-3.1-8b-instant` |
| `Ai__ApiKey` | your Groq API key (secret) |

> Note the **double underscores** — that's how ASP.NET maps env vars to `Section:Key`.
> The model only classifies a question into a fixed intent; it never sees tenant data, so a
> small/free model is plenty. Leave `Ai__Enabled` unset to run the disabled no-op assistant.

Deploy, then copy the service URL, e.g. `https://ofren-collect-api.onrender.com`. Check `…/health`.

## 3. SPA — Vercel

1. [vercel.com](https://vercel.com) → **Add New → Project** → import the repo.
2. **Root Directory: `web`** · Framework preset: **Vite** (build `npm run build`, output `dist`).
3. Environment variable: `VITE_API_URL` = your Render API URL (from step 2).
4. Deploy, then copy the SPA URL, e.g. `https://ofren.vercel.app`.

(`web/vercel.json` handles SPA deep-link routing; `web/public/_redirects` does the same on Netlify.)

## 4. Close the loop — CORS

Back on **Render**, set `Cors__Origin` to the exact Vercel URL from step 3 and redeploy. This lets the browser call the API and open the SignalR WebSocket (it needs the exact origin + credentials).

## 5. Monnify webhook

In your Monnify sandbox dashboard, set the webhook URL to:

```
https://<your-render-app>.onrender.com/api/webhooks/monnify
```

A sandbox payment into a reserved account now reconciles automatically and the dashboard badge flips live.

## 6. Continuous deployment — CI-gated (GitHub Actions)

The workflow in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) runs the compulsory gate
on every push/PR to `main` (build, `dotnet format`, all tests on real Postgres, the **≥90% coverage
floor**, and the SPA type-check/build). Deployment fires **only after that gate is green**, on
pushes to `main`. Wire it once:

1. **Create deploy hooks** (a URL that triggers a build when POSTed to):
   - **Render** → your service → *Settings → Deploy Hook* → copy the URL.
   - **Vercel** → project → *Settings → Git → Deploy Hooks* → create one for `main` → copy the URL.
2. **Add them as GitHub Actions secrets** (repo → *Settings → Secrets and variables → Actions*):
   - `RENDER_DEPLOY_HOOK_URL`
   - `VERCEL_DEPLOY_HOOK_URL`

   > If a secret is absent, that target is simply skipped — the gate still runs. So you can wire
   > one, both, or neither.
3. **Turn off each platform's native git auto-deploy** so CI is the *only* path to production
   (otherwise a red build would still deploy):
   - **Render** → *Settings → Build & Deploy → Auto-Deploy → No*.
   - **Vercel** → *Settings → Git → Ignored Build Step* → set to `exit 0` (never build on push),
     leaving the deploy hook as the trigger.

After this, every push to `main` must pass build + format + tests + 90% coverage before Render and
Vercel are told to deploy. A red gate blocks the release.

> **Reminder (CLAUDE.md §13):** never lower `COVERAGE_MIN` or skip a test to go green — fix the cause.

## 7. (Optional) Keep it awake

Render's free service sleeps after ~15 min idle (cold start ~30–60s, and Monnify's first webhook may hit a cold instance — the durable inbox + Monnify retries recover it). To keep it warm for a demo, add a free monitor (e.g. [UptimeRobot](https://uptimerobot.com)) pinging `https://<app>.onrender.com/health` every 5–10 minutes.

---

## Verify

Open the Vercel URL and log in with the seeded owner: **ada@brightpath.ng / password123**. You should see the live dashboard. Create a plan, enrol a customer (provisions a real reserved account), pay into it via the Monnify simulator, and watch the badge flip.
