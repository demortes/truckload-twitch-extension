# Third-party notices

Truckload is MIT licensed (see [LICENSE](LICENSE)). It uses, bundles or interoperates with the following.
Exact dependency versions are in the lock and project files (`src/package-lock.json`, the `.csproj` files).

## Bundled in release downloads

### TruckTel (installed by the Windows installer)

The installer redistributes the TruckTel game plugin (`trucktel.dll`) together with its license, as its author
[asks](https://github.com/jvanstraten/TruckTel/blob/main/doc/app.md). Source: <https://github.com/jvanstraten/TruckTel>.

```
MIT License

Copyright (c) 2026 Jeroen van Straten

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The authoritative text is the `LICENSE` file shipped with the plugin (`plugins/trucktel/LICENSE`).

### Datadog .NET tracer (backend container image)

The backend image includes the Datadog .NET tracer via the `Datadog.Trace.Bundle` package, licensed under the
Apache License 2.0 (<https://github.com/DataDog/dd-trace-dotnet>).

## Libraries (not modified)

| Component | License |
|---|---|
| .NET runtime and ASP.NET Core, Entity Framework Core | MIT |
| Npgsql and the Npgsql EF Core provider | PostgreSQL License |
| OpenTelemetry .NET (SDK, OTLP exporter, instrumentation) | Apache-2.0 |
| React, React DOM, Vite and the frontend build tooling | MIT |

The Windows installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php).

## Interoperability only (nothing from these is redistributed)

- **SCS Software's telemetry SDK**: only the public channel names (for example `truck.speed`) are used, to read
  data from TruckTel. No SDK files are included.
- **Funbit's ETS2/ATS Telemetry Server** and the **Trucky** API: the Bridge can read Funbit-compatible JSON,
  and an optional (disabled) convoy feature references the Trucky API. Neither is bundled.

## Trademarks and affiliation

"American Truck Simulator" and "Euro Truck Simulator 2" are trademarks of SCS Software s.r.o., and "Twitch" is
a trademark of Twitch Interactive, Inc. Truckload is an independent fan project and is **not affiliated with,
endorsed by, or sponsored by** SCS Software, Twitch Interactive, or the authors of TruckTel.
