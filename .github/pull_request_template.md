## What and why

<!-- What does this change, and why? Link the issue it fixes, if any. -->

## Checklist

- [ ] Tests added or updated for behavior changes (`dotnet test Truckload.slnx`)
- [ ] Frontend: `npm run lint` and `npm run build` pass (in `src/`)
- [ ] Payload changes are in `shared/Truckload.Contracts`, `worker/src/lib/contract.ts` and `docs/telemetry-contract.md` (the Worker must be deployed before a Bridge that sends a new field)
- [ ] No secrets, ingest keys, tokens or personal data in the diff, logs or screenshots
