# Truckload Worker

The Truckload backend (the Twitch extension's EBS) as a Cloudflare Worker: a [Hono](https://hono.dev) API on
**D1** (SQLite), with the landing page and docs site served as **static assets** from the same origin. It is
a behavior-for-behavior port of `backend/Truckload.Ebs` (ASP.NET Core + Postgres); the bridge, the extension
frontend and the wire contract (`docs/telemetry-contract.md`) are unchanged.

```
POST /api/ingest                    bridge -> store latest snapshot, archive finished jobs, relay over Twitch PubSub
GET  /api/telemetry/:channelId      latest snapshot (public, no auth)
GET  /api/telemetry/:channelId/history?limit=   recent completed jobs
GET  /api/channels/keys             the broadcaster's ingest key   (Twitch extension JWT, role=broadcaster)
POST /api/channels/keys             generate or rotate the key     (same)
GET  /api/health, /api/health/live, /api/health/ready
GET  /                              landing page (static)
GET  /about/                        the docs/ marketing page (static, copied from ../docs by `npm run sync:static`)
```

## Layout

| Path | What |
| --- | --- |
| `src/index.ts`, `src/routes/*` | Hono app, one module per former controller, mounted at the same paths |
| `src/lib/contract.ts` | The v1 contract: parsing, validation, canonical JSON (port of `Truckload.Contracts`) |
| `src/lib/twitch.ts` | Extension JWTs (HS256 via WebCrypto) and the PubSub broadcast |
| `src/lib/store.ts` | D1 queries: channels, snapshot upsert (with the broadcast throttle), job history |
| `migrations/0001_init.sql` | The EF model as SQL |
| `public/` | Static assets. `about/`, `logo.png` and `favicon.ico` are generated from `../docs` and gitignored |
| `test/` | vitest. D1 is real SQLite (`node:sqlite`) behind a small adapter, so the SQL and migration are exercised |

## Develop

```
cd worker
npm install
cp wrangler.example.jsonc wrangler.jsonc      # gitignored; holds your own D1 id
printf 'TWITCH_CLIENT_ID=local\nTWITCH_EXTENSION_SECRET=<base64>\nBROADCAST_ENABLED=false\n' > .dev.vars
npm run db:migrate:local
npm run dev                                   # http://localhost:8787
npm run typecheck && npm test
```

`BROADCAST_ENABLED=false` stores telemetry but never calls Twitch, which is what you want locally. Point the
bridge at it with `--ingest-url http://localhost:8787/api/ingest --key <key>` (`--demo` needs no game).
Restart `wrangler dev` after editing `.dev.vars`.

## Configuration

Secrets (`wrangler secret put <NAME>`, or `.dev.vars` locally):

| Name | Meaning |
| --- | --- |
| `TWITCH_CLIENT_ID` | The extension's client id |
| `TWITCH_EXTENSION_SECRET` | The extension's Base64 secret. Verifies broadcaster JWTs and signs PubSub JWTs |

Optional variables: `BROADCAST_ENABLED` (`"false"` disables Twitch relay), `MIN_BROADCAST_INTERVAL_MS`
(default 1000). Bindings: `DB` (D1), `ASSETS`, `INGEST_LIMITER`, `KEYS_LIMITER` (see
`wrangler.example.jsonc`). Keep `src/env.ts`, `wrangler.example.jsonc` and this table in step.

The repository is public, so nothing account-specific is committed: the D1 id lives in your own
`wrangler.jsonc` (gitignored) or in the `CLOUDFLARE_D1_DATABASE_ID` GitHub secret.

## Deploy

CI (`.github/workflows/worker.yml`) typechecks, tests and dry-run bundles on every pull request, and on
`main` applies D1 migrations and deploys, if the three repository secrets (`CLOUDFLARE_API_TOKEN`,
`CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_D1_DATABASE_ID`) are set. Without them the deploy job skips itself.

A deploy publishes the Worker on its `workers.dev` address. It does **not** move any custom domain.

### First-time setup

1. `npx wrangler d1 create truckload`, then put the printed id in `wrangler.jsonc` and the GitHub secret.
2. `npx wrangler secret put TWITCH_CLIENT_ID` and `... TWITCH_EXTENSION_SECRET` (use the existing extension's values).
3. `npm run db:migrate:remote`, then `npm run deploy`.
4. Verify on the `workers.dev` URL: `/api/health` is `ok` with `db: ok`, `/` renders, an unauthenticated
   `GET /api/channels/keys` is 401, and a bridge in `--demo` mode reaches `stored: true`.

### Cutover from the .NET backend (a separate, deliberate step)

The database starts **empty**, so every streamer must generate a new ingest key on the extension's Config
page and update their bridge. Do these in order, and not before step 4 above is green:

1. Add `truckload.demortes.com` to the Worker as a Custom Domain (the zone must be on Cloudflare), replacing
   the old A/AAAA record that pointed at the VM. The extension's EBS URL in the Twitch console stays the same.
2. Confirm `https://truckload.demortes.com/api/health` is served by the Worker and that a channel's
   `Config` page can generate a key.
3. Stop the old stack (`deploy/` Compose or `k8s/`). Keep it until you are sure.
4. Delete the legacy backend in a follow-up change once the Worker has been live for a while.

## What differs from the .NET backend

- **Rate limits** use Workers Rate Limiting bindings: per ingest key (50 requests per 10 seconds, a
  malformed-key bucket shared by everything that is not a 64-hex key) and per broadcaster for key
  management. They are approximate and per Cloudflare location. The old global per-endpoint ceilings are
  dropped; Cloudflare absorbs floods at the edge.
- **Broadcast throttle** (one PubSub message per channel per interval, and the back-off after a Twitch 429)
  lives in the snapshot row the ingest upsert already writes, so it is atomic across isolates and costs no
  extra write.
- **Body size** is capped in code at 4096 bytes (declared and streamed); there is no Kestrel limit to rely on.
- **Observability**: Datadog and OpenTelemetry are gone. Structured JSON lines (`event: ingest`,
  `pubsub_*`) go to Workers Observability, which is enabled in `wrangler.jsonc`.
- **Landing page** is a static file (`public/index.html`); its three links are plain HTML instead of
  `Site:*` settings, and the ingest URL is filled in by `landing.js` from the page origin.
- **Health** reports version `2.0.0`.
- Error bodies keep the original `{ "error": ..., "details": null }` shape, and `details` is a string only
  for validation failures.