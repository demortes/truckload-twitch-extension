# Deploying Truckload — Single VM

This is the supported deployment path for v1: one VM running Docker Compose,
with [Caddy](https://caddyserver.com/) as a reverse proxy that automatically
obtains and renews a Let's Encrypt TLS certificate. Twitch requires the
backend (EBS) to be served over HTTPS on a public domain, so this is the
minimum viable setup.

(A Kubernetes path also exists under `k8s/`, but it is secondary and less
exercised — prefer this one unless you already run a cluster.)

## Prerequisites

- A VM (any cloud provider, or bare metal) with Docker and the Compose plugin
  installed, and ports 80 and 443 open to the internet.
- A domain name with an A (or AAAA) record pointing at the VM's public IP.
- The backend image published to GHCR (see the release workflow in
  `.github/workflows/release.yml`), or build it locally on the VM.

## Steps

```bash
git clone https://github.com/demortes/truckload-twitch-extension.git
cd truckload-twitch-extension/deploy
cp .env.example .env
# edit .env: DOMAIN, ACME_EMAIL, GHCR_OWNER, POSTGRES_PASSWORD,
# TWITCH_EXTENSION_CLIENT_ID, TWITCH_EXTENSION_SECRET

docker compose pull
docker compose up -d
```

Caddy will request a certificate for `DOMAIN` automatically the first time it
starts (this requires ports 80/443 to already be reachable from the internet).

## Verify

```bash
curl https://$DOMAIN/api/health
# {"status":"ok","version":"1.0.0","db":"ok"}

docker compose logs backend --tail=50   # confirm migrations applied cleanly
```

Then, from the extension's Config page (built with
`VITE_BACKEND_URL=https://$DOMAIN`), generate an ingest key and run the bridge
app against `https://$DOMAIN/api/ingest`.

## Upgrading

```bash
cd deploy
# bump TAG in .env if you're pinning versions
docker compose pull
docker compose up -d
```

EF Core migrations run automatically on startup (`Database__AutoMigrate=true`).

## Backups

```bash
docker compose exec db pg_dump -U truckload truckload > backup-$(date +%F).sql
```

## Rollback

Set `TAG` in `.env` back to a previous release tag and `docker compose up -d`
again.

## Twitch Developer Console

Once the backend is reachable over HTTPS, add its domain to **Capabilities →
Allowlist for URL Fetching Domains** for your extension version — without
this, the frontend's `fetch()` calls to the backend are blocked by Twitch's
Content Security Policy. See `docs/twitch-console-checklist.md` for the full
list of console settings.
