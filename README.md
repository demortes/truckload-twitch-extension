<p align="center">
  <img src="docs/assets/logo.png" alt="Truckload logo" width="96" height="96" />
</p>

<h1 align="center">Truckload</h1>

A Twitch Extension for American Truck Simulator and Euro Truck Simulator 2
streamers. Viewers see the streamer's current job and truck stats live,
right on the stream.

## Architecture

```
ATS / ETS2  →  TruckTel (or a Funbit-compatible server)  →  Truckload Bridge  →  Worker (EBS)  →  Twitch Extension PubSub  →  Viewers
                  (local game plugin)                        (bridge/)           (worker/)
```

- **`bridge/Truckload.Bridge`** — the console app the streamer runs on their PC. Polls the local ETS2/ATS
  telemetry server, normalizes the data, and sends it to the backend. **`bridge/Truckload.Bridge.Tray`** is the
  same bridge as a Windows tray app with a status light, first-run window and update prompts (the Windows
  installer sets it up). See `bridge/README.md`.
- **`worker/`** — the extension backend (EBS) as a **Cloudflare Worker** (Hono + TypeScript) on **D1**, with the
  landing page and docs site served as static assets from the same origin. It validates and stores the latest
  telemetry per channel, archives completed jobs, manages ingest keys, and relays telemetry to viewers over
  Twitch's Extension PubSub. See `worker/README.md`.
- **`shared/Truckload.Contracts`** — the canonical telemetry payload contract used by the bridge. The Worker
  mirrors it in `worker/src/lib/contract.ts`. See `docs/telemetry-contract.md`.
- **`src/`** — the React/Vite frontend: five views (panel, video overlay, video component, mobile, and the
  broadcaster's config page). Twitch hosts it, not Cloudflare.

## For streamers

See `docs/streamer-setup.md`.

## Development

### Worker (Node 24, Cloudflare Workers)

```bash
cd worker
npm ci
cp wrangler.example.jsonc wrangler.jsonc     # gitignored; holds your own D1 id
# create .dev.vars in this folder with TWITCH_CLIENT_ID, TWITCH_EXTENSION_SECRET (any base64 32 bytes)
# and BROADCAST_ENABLED=false
npm run typecheck && npm test
npm run db:migrate:local
npm run dev                                  # http://localhost:8787 (leave it running)
```

In a second terminal, from the **repository root**, mint a dev broadcaster JWT to exercise the key-management
endpoint without a real Twitch context:

```bash
node scripts/mint-dev-jwt.mjs --channel 12345 --secret <the same base64 secret>
```

Then run the bridge against the Worker in demo mode (no game required), also from the repository root:

```bash
dotnet run --project bridge/Truckload.Bridge -- --demo --ingest-url http://localhost:8787/api/ingest --key <key from POST /api/channels/keys>
```
`worker/TESTING.md` has the full local-test, deploy and smoke-test walkthrough.

### Bridge (.NET 10)

```bash
dotnet restore Truckload.slnx
dotnet build Truckload.slnx
dotnet test Truckload.slnx          # bridge and contract tests
```

### Frontend (Node 22, React 19, Vite)

```bash
cd src
npm install
npm run dev     # serves all five HTML entries
npm run lint
npm run build
```

Without a Twitch extension context (`window.Twitch.ext`), every view falls
back to shared mock data (`src/src/dev/mockTelemetry.js`) so the UI is
inspectable on its own.

To see a real channel's **live production data** in a plain browser tab, open any view with the channel id, for
example `http://localhost:5173/overlay.html?channel=9285`. It talks to `https://truckload.demortes.com` unless you
add `&backend=<url>` (for example `&backend=http://localhost:8787` for a local Worker). To make every page
from `npm run dev` live without query parameters, put this in `src/.env.development.local` (gitignored):

```
VITE_DEV_CHANNEL=<your twitch channel id>
VITE_DEV_BACKEND=<optional; defaults to production>
```

A strip at the bottom of the page says what you are looking at: mock data or live, which backend and channel,
how old the data is, and why nothing is showing (no data for the channel yet, an unreachable backend, or a
bridge that has stopped sending). Note that the overlay and video component hide themselves when the data is
more than two minutes old, so use the banner to tell "hidden on purpose" from "broken". The banner and this
mode exist only in the dev server, never in release builds.

## Building for release

- Bridge: `dotnet publish bridge/Truckload.Bridge -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
- Tray app and installer: `installer/build.ps1` (needs Inno Setup 6).
- Frontend: `cd src && npm run build && npm run zip` produces
  `truckload-extension.zip`, ready to upload to the Twitch Developer Console.

`.github/workflows/release.yml` builds the bridge zips, the tray zip, the installer and the frontend zip on a
`v*` tag push and publishes them as GitHub release assets.

## Deployment

Production is the Cloudflare Worker. `.github/workflows/worker.yml` runs typecheck, tests and a dry-run bundle
on every pull request, and on `main` applies D1 migrations and deploys (it needs the `CLOUDFLARE_API_TOKEN`,
`CLOUDFLARE_ACCOUNT_ID` and `CLOUDFLARE_D1_DATABASE_ID` repository secrets, and skips itself without them).
First-time setup, secrets, custom domain and rollback are in `worker/README.md` and `worker/TESTING.md`.
## Twitch extension review

See `docs/twitch-console-checklist.md` and `docs/twitch-review-guide.md`.

## What's out of scope for v1

Convoy/TruckersMP status (code is present but feature-flagged off — see
`src/src/config/features.js`) and historical telemetry storage.

## Contributing and security

See [CONTRIBUTING.md](CONTRIBUTING.md). Please report security problems privately as described in
[SECURITY.md](SECURITY.md), not as public issues.

## License and disclaimer

[MIT](LICENSE). Third-party components (including the bundled TruckTel plugin) are listed in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Truckload is an independent fan project. It is **not affiliated with, endorsed by, or sponsored by SCS Software
or Twitch Interactive**. "American Truck Simulator" and "Euro Truck Simulator 2" are trademarks of SCS Software
s.r.o.; "Twitch" is a trademark of Twitch Interactive, Inc.
