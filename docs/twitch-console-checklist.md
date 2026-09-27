# Twitch Developer Console checklist

Settings to configure at [dev.twitch.tv/console/extensions](https://dev.twitch.tv/console/extensions)
for your extension. This is a one-time, manual setup — nothing here is
automated by this repo.

## Extension type

Enable all four view types this release ships:

- Panel
- Video Overlay
- Video Component
- Mobile

## Asset hosting (per version)

| View | File | Notes |
|---|---|---|
| Panel | `panel.html` | Height ~500px, matches `panel.css`. |
| Video Overlay | `overlay.html` | Full-bleed transparent overlay. |
| Video Component | `component.html` | Roughly 2:1 aspect; "Zoom" scaling on. |
| Mobile | `mobile.html` | |
| Config | `config.html` | Shown on the broadcaster's dashboard only. |

Upload the zip produced by `npm run build && npm run zip` in `src/` (or the
release asset from CI).

## Capabilities

- **Allowlist for URL Fetching Domains**: add your backend's HTTPS domain
  (e.g. `https://truckload.example.com`). Without this, the frontend's
  `fetch()` calls to `/api/telemetry/...` and `/api/channels/keys` are
  blocked by Twitch's Content Security Policy.
- No Identity Link, Bits, or other capabilities are required.

## Credentials

- Copy the **Client ID** into the backend's `Twitch__ClientId`.
- Copy the **Extension Secret** (base64, as shown in the console) into the
  backend's `Twitch__ExtensionSecret`.

## Lifecycle

1. **Local Test** — run `npm run dev` with `VITE_HTTPS=1` (or a local reverse
   proxy) so the Testing Base URI can be `https://localhost:5173/`.
2. **Hosted Test** — upload the release zip; test on a real channel.
3. **Review** — submission requires a walkthrough (see
   `docs/twitch-review-guide.md`) and a test channel where the reviewer can
   see live data. Run `Truckload.Bridge.exe --demo` against the production
   backend with that channel's real ingest key for the duration of review.
4. **Released** — once approved, the extension is available to all channels.

## Privacy

The backend stores only: a channel ID, an ingest key, and the latest
telemetry snapshot for that channel. No viewer data is collected. State this
plainly in the extension's privacy policy field if one is required.
