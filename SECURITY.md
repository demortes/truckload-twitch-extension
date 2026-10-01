# Security policy

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

- Use GitHub's private reporting: **Security → Report a vulnerability** on this repository, or
- email **truckload@demortes.com**.

Include what you found, how to reproduce it, and the version (release tag) you tested. This is a
hobby-scale project maintained by one person, so responses are best effort, but reports are read and
taken seriously.

**Never paste a real ingest key, extension secret, JWT, or database credential** into an issue, a pull
request, or an email. If you accidentally exposed one, regenerate it (ingest keys: the extension's Config
page) and tell the maintainer.

## Scope

In scope:

- the backend (`backend/`, the extension backend service / EBS) and its API,
- the Bridge (`bridge/`) and the Windows installer (`installer/`),
- the Twitch extension frontend (`src/`),
- the deployment files (`deploy/`, `k8s/`) and CI workflows (`.github/`).

Out of scope: vulnerabilities in the games, the SCS SDK, [TruckTel](https://github.com/jvanstraten/TruckTel),
Twitch itself, or third-party dependencies (report those upstream; tell us if a fix needs a version bump here).

## Supported versions

Only the latest release is supported.

## Things worth knowing

- Each channel has one **ingest key** (256-bit random). It is accepted only in the `X-Api-Key` header, never in
  a URL, and the ingest and key-management endpoints are rate limited.
- The telemetry for a channel is readable without authentication at `GET /api/telemetry/{channelId}`. That is
  by design (it is the same data viewers see in the extension); it contains no viewer data.
- The backend stores only a channel ID, its ingest key, the latest telemetry snapshot, and completed-job history.
