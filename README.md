# Truckload

A Twitch Extension for American Truck Simulator and Euro Truck Simulator 2
streamers. Viewers see the streamer's current job and truck stats live,
right on the stream.

## Architecture

```
ATS / ETS2  →  local telemetry server  →  Truckload Bridge  →  Backend (EBS)  →  Twitch Extension PubSub  →  Viewers
                (Funbit-compatible)         (bridge/)            (backend/)
```

- **`bridge/Truckload.Bridge`** — a small Windows console app the streamer
  runs on their PC. Polls the local ETS2/ATS telemetry server, normalizes the
  data, and sends it to the backend. See `bridge/README.md`.
- **`backend/Truckload.Ebs`** — the extension backend service (ASP.NET Core +
  PostgreSQL). Validates and stores the latest telemetry per channel, and
  relays it to viewers over Twitch's Extension PubSub.
- **`shared/Truckload.Contracts`** — the canonical telemetry payload contract
  shared by the backend and the bridge, so they can't drift apart. See
  `docs/telemetry-contract.md`.
- **`src/`** — the React/Vite frontend: five views (panel, video overlay,
  video component, mobile, and the broadcaster's config page).

## For streamers

See `docs/streamer-setup.md`.

## Development

### Backend + bridge (.NET 10)

```bash
dotnet restore Truckload.slnx
dotnet build Truckload.slnx
dotnet test Truckload.slnx
```

Run the backend locally against Postgres in Docker:

```bash
cd backend
cp .env.example .env   # fill in a Twitch client id + base64 secret (any 32 random bytes work for local dev)
docker compose up -d
curl http://localhost:25846/api/health
```

Mint a dev broadcaster JWT to exercise the key-management endpoint without a
real Twitch context:

```bash
node scripts/mint-dev-jwt.mjs --channel 12345 --secret <the same base64 secret>
```

Run the bridge against it in demo mode (no game required):

```bash
dotnet run --project bridge/Truckload.Bridge -- --demo --ingest-url http://localhost:25846/api/ingest --key <key from the Config page/API>
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

## Building for release

- Backend image: `docker build -f backend/Truckload.Ebs/Dockerfile .` (from
  the repo root).
- Bridge: `dotnet publish bridge/Truckload.Bridge -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
- Frontend: `cd src && npm run build && npm run zip` produces
  `truckload-extension.zip`, ready to upload to the Twitch Developer Console.

`.github/workflows/release.yml` does all three on a `v*` tag push and
publishes them as GitHub release assets / a GHCR image.

## Deployment

See `deploy/DEPLOY.md` for the supported single-VM (Docker Compose + Caddy)
deployment. `k8s/` holds a secondary, less-exercised Kubernetes path.

## Twitch extension review

See `docs/twitch-console-checklist.md` and `docs/twitch-review-guide.md`.

## What's out of scope for v1

Convoy/TruckersMP status (code is present but feature-flagged off — see
`src/src/config/features.js`), further Kubernetes polish, non-Windows bridge
builds, and historical telemetry storage.
