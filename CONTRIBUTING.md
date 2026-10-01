# Contributing

Thanks for your interest in Truckload! Bug reports, ideas and pull requests are welcome.

## Before you start

- For anything bigger than a small fix, **open an issue first** so we can agree on the approach.
- **Security problems:** do not use a public issue; see [SECURITY.md](SECURITY.md).
- Never include real ingest keys, secrets or tokens in issues, logs or screenshots.

## Getting set up

The README has the full instructions. In short:

```bash
dotnet restore Truckload.slnx
dotnet test Truckload.slnx          # backend + bridge tests

cd src && npm ci
npm run lint && npm run build       # frontend
```

You do not need the games to work on most of this: the Bridge has a `--demo` mode, and the frontend falls back
to mock data when there is no Twitch extension context (see the README).

## Pull requests

- Keep each PR focused, and describe **what changed and why**.
- Add or update tests for behavior changes (`backend/Truckload.Ebs.Tests`, `bridge/Truckload.Bridge.Tests`).
- CI must pass: .NET build and tests, frontend lint and build, the Docker image build, and the installer build.
- Changes to the telemetry payload belong in `shared/Truckload.Contracts` and `docs/telemetry-contract.md`.
  The contract is additive: the backend rejects unknown fields, so the **backend must be deployed before a
  Bridge that sends a new field**.
- UI changes: please check them at the real Twitch sizes (panel 318x500, overlay, mobile, video component) and keep
  them accessible (keyboard operable, readable contrast, no `aria-live` on fast-changing values).
- Match the surrounding code's style and comment density.

## Code of conduct

Be kind and assume good faith. Harassment or abuse isn't tolerated.

## License

By contributing you agree that your contribution is licensed under the project's [MIT License](LICENSE).
