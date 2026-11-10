# Deploy and test the Worker on Cloudflare

A step-by-step guide, written for PowerShell or Git Bash on Windows (the commands work in both). Do the parts in
order. Part 1 needs no Cloudflare account; part 2 deploys a **throwaway test copy** on a `workers.dev` address
that cannot affect your production backend. Moving `truckload.demortes.com` over is a separate, later step
(see "Going live" at the end and the cutover list in [README.md](README.md)).

What has and has not been verified so far: the code, the SQL and the whole API flow are tested locally on the
real Workers runtime (`workerd`), and the real C# bridge was run against it. **No deploy to a real Cloudflare
account has been done yet**, so part 2 is the first time that path runs; the troubleshooting table at the
end lists what could go wrong.

## Part 0: Prerequisites

- Node 24+ and npm (check with `node --version`).
- A Cloudflare account (the free plan is enough to test).
- From the repo root: `cd worker` then `npm ci`.

## Part 1: Test locally (no account needed)

```
cd worker
cp wrangler.example.jsonc wrangler.jsonc
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

Copy the printed value. It is your local **test secret**. Create `worker/.dev.vars` (gitignored):

```
TWITCH_CLIENT_ID=local-test
TWITCH_EXTENSION_SECRET=<paste the value here>
BROADCAST_ENABLED=false
```

Then, in one terminal:

```
npm run db:migrate:local
npm run dev
```

and in a second terminal (same folder):

```
npm run typecheck
npm test
npm run smoke -- --url http://localhost:8787 --secret <the same value>
```

Expected: typecheck clean, all tests pass, and the smoke test ends with `All checks passed.` (14 checks).
Open <http://localhost:8787/> in a browser to see the landing page and <http://localhost:8787/about/> for the docs site.

Try the real bridge against it (a demo job, no game needed). Generate a key first:

```
node ../scripts/mint-dev-jwt.mjs --channel 12345 --secret <the same value>
```

Use that token to create a key:

```
curl -X POST -H "Authorization: Bearer <token>" http://localhost:8787/api/channels/keys
```

Then run the bridge with the `ingestKey` from the response:

```
cd ../bridge
dotnet run --project Truckload.Bridge -- --demo --verbose --ingest-url http://localhost:8787/api/ingest --key <ingestKey>
```

and check `curl http://localhost:8787/api/telemetry/12345`.

## Part 2: Deploy a test copy to Cloudflare

All commands run in `worker/`. Nothing here touches your production domain.

### 2.1 Log in

```
npx wrangler login
```

A browser window opens; approve it. (`npx wrangler whoami` confirms the account.)

### 2.2 Create the database

```
npx wrangler d1 create truckload
```

It prints a `database_id`. Open `worker/wrangler.jsonc` (your gitignored copy) and replace
`REPLACE_WITH_YOUR_D1_DATABASE_ID` with it. Wrangler may offer to add the binding for you; either way, the
`d1_databases` entry must have `"database_name": "truckload"`.

Check the two rate limiter `namespace_id` values (`"1001"`, `"1002"`). They are just numbers you choose; if the
deploy later complains that one is already used by another Worker in your account, change them.

### 2.3 Create the tables and deploy

```
npm run db:migrate:remote
npm run deploy
```

`db:migrate:remote` asks for confirmation before applying all pending migrations (`0001_init.sql`, `0002_broadcast_token.sql`) to the remote database. The deploy
prints the Worker's address, like `https://truckload.<your-subdomain>.workers.dev`. If this is your first
Worker on the account, wrangler may ask you to register a `workers.dev` subdomain; accept.

If you already applied the schema before pulling the latest commit, run `npm run db:migrate:remote` (and `npm run db:migrate:local` locally) again: it applies the new `0002_broadcast_token.sql` and skips what is already there.

Right after the first deploy, `/api/health` and the static pages already work, but anything that verifies a
Twitch token needs the secrets from the next step.

### 2.4 Set the secrets

For a **test copy**, use a throwaway secret you generate yourself (so you can mint tokens locally) and turn
the Twitch relay off so nothing is sent to Twitch:

```
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

Save that value somewhere private, then enter each of these at the masked prompt (paste, press Enter):

```
npx wrangler secret put TWITCH_EXTENSION_SECRET
npx wrangler secret put TWITCH_CLIENT_ID
npx wrangler secret put BROADCAST_ENABLED
```

Use the throwaway secret, any text such as `test` for the client id, and `false` for `BROADCAST_ENABLED`.
Secrets apply immediately and survive later deploys. Never paste real secrets into a chat or a file in the repo.

### 2.5 Run the smoke test against it

```
npm run smoke -- --url https://truckload.<your-subdomain>.workers.dev --secret <the throwaway secret> --channel 12345
```

Expected: `All checks passed.` It creates a test channel and key and writes two snapshots. A `401` on the
viewer-token check means the secret you passed differs from the one set on the Worker.

### 2.6 Test with the real bridge and the extension code

Get a key (the smoke test prints none, so mint one):

```
node ../scripts/mint-dev-jwt.mjs --channel 12345 --secret <the throwaway secret>
curl -X POST -H "Authorization: Bearer <token>" https://truckload.<your-subdomain>.workers.dev/api/channels/keys
```

Run the bridge in demo mode (or enter the same URL and key in the tray app's Settings):

```
cd ../bridge
dotnet run --project Truckload.Bridge -- --demo --verbose --ingest-url https://truckload.<your-subdomain>.workers.dev/api/ingest --key <ingestKey>
```

See the overlay with live data, without Twitch, using the dev-only mode (the extension dev server):

```
cd ../src
npm run dev
```

then open
`http://localhost:5173/overlay.html?channel=12345&backend=https://truckload.<your-subdomain>.workers.dev`.
It polls the Worker every 2 seconds. The Worker allows any origin, so this works from `localhost`.

### 2.6b Watch it live

```
cd ../worker
npx wrangler tail truckload
```

Each ingest logs one JSON line (`event: ingest`, `outcome: stored`). The same logs are in the Cloudflare
dashboard under Workers & Pages > truckload > Observability.

## Part 3: Test the real Twitch path

This needs your **real** extension values, so do it only when part 2 is green, and still on the `workers.dev`
address (the old backend keeps serving production):

1. Put the real values on the Worker (masked prompts): `npx wrangler secret put TWITCH_EXTENSION_SECRET`
   and `... TWITCH_CLIENT_ID` (both from the extension's page in the Twitch developer console). Remove the
   test override with `npx wrangler secret delete BROADCAST_ENABLED` so broadcasting is on.
2. Build the extension pointed at the test Worker, then upload it as a test version:
   ```
   cd ../src
   $env:VITE_BACKEND_URL = "https://truckload.<your-subdomain>.workers.dev"   # PowerShell
   npm run build
   ```
   (Git Bash: `VITE_BACKEND_URL=https://... npm run build`.) Zip the contents of `dist/` and upload it to a new
   extension version in the Twitch console.
3. In that version's settings, add the `workers.dev` host to **Allowlist for URL Fetching Domains** (Twitch
   blocks extension network calls to hosts that are not listed).
4. Install the test version on your channel (Hosted Test), open the extension's Config page from the Creator
   Dashboard, click **Generate API Key**, and put that key in the bridge.
5. Run the bridge (demo or the game). The panel should show live data; a PubSub relay problem shows up as
   `broadcast: failed` in `wrangler tail` and in the bridge log.

Not yet verified by me: this whole part, in particular the Twitch PubSub call with a real secret and the
Config page against the Worker.

## Part 4: Automatic deploys from GitHub (optional)

The workflow `.github/workflows/worker.yml` runs tests on every PR and, on `main`, applies migrations and
deploys. It needs three repository secrets; until they exist the deploy job skips itself.

1. Cloudflare dashboard > My Profile > API Tokens > Create Token > use the **Edit Cloudflare Workers** template
   and add **Account > D1 > Edit**. Copy the token.
2. Set the secrets (each command prompts for the value, so nothing lands in your shell history):
   ```
   gh secret set CLOUDFLARE_API_TOKEN
   gh secret set CLOUDFLARE_ACCOUNT_ID
   gh secret set CLOUDFLARE_D1_DATABASE_ID
   ```
   The account id is on the dashboard's Workers & Pages page; the D1 id is the `database_id` from step 2.2.

## Going live

When you are happy, follow "Cutover from the .NET backend" in [README.md](README.md). In short: set the real
secrets (part 3), add `truckload.demortes.com` to the Worker as a Custom Domain, confirm
`/api/health`, stop the old stack, and tell streamers to generate a **new** key (the database starts empty).

## Rolling back and cleaning up

- Undo a bad deploy: `npx wrangler rollback` (pick the previous version).
- Throw the test copy away: `npx wrangler delete truckload` and `npx wrangler d1 delete truckload`.
- The old backend is untouched by all of this until you move the domain.

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| `/api/health` returns 503 or `db: error` | Migrations were not applied to the remote database | `npm run db:migrate:remote` |
| Smoke test: viewer check says HTTP 401 | The `--secret` differs from `TWITCH_EXTENSION_SECRET` on the Worker | Pass the same value, or re-run `wrangler secret put` |
| Key endpoints return 500 | `TWITCH_EXTENSION_SECRET` is missing or not valid Base64 | Set it again (Base64 only) |
| Deploy fails on the `ratelimits` binding | A `namespace_id` is already used in your account | Change `"1001"` / `"1002"` in `wrangler.jsonc` |
| `wrangler dev` unreachable or stuck on Windows | Stale `workerd`/`node` processes from an earlier run | Stop them, then start `npm run dev` again; restart after editing `.dev.vars` |
| Ingest shows `broadcast: failed` | Wrong client id/secret, or the extension is not allowed to relay | Check the secrets and `wrangler tail` for the `pubsub_failed` line (it includes Twitch's reply) |
| Extension in Twitch cannot reach the Worker | Host missing from the URL-fetching allowlist, or the build used the wrong `VITE_BACKEND_URL` | Fix both; rebuild and re-upload |
| Overlay dev mode shows nothing | No data for that channel yet, or data older than 90 seconds | Start the bridge (`--demo` is fine) and reload |