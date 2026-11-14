# PrintDeck

A modern 3D-printer host in .NET 11 / Blazor Server — an OctoPrint-style web UI for Marlin printers over USB serial.

## Features

- Connect over serial (auto-discovers `/dev/ttyUSB*`, `/dev/ttyACM*`, `/dev/serial/by-id/*`) or the built-in **virtual printer**
- Marlin host protocol: line numbers + checksums, resend handling, `busy:` tolerance, `M155` auto-report with `M105` polling fallback
- Dashboard: job progress with elapsed / remaining / finish time, pause / resume / cancel, live temperatures, presets, history chart, MJPEG camera
- Control: XY/Z jog with step sizes, homing, extrude/retract (cold-extrusion guard), fan, motors off
- Files: drag-and-drop upload with analysis (print time, filament, layers) from OrcaSlicer, PrusaSlicer and Cura metadata
- G-code viewer: 2D layer view and 3D view (three.js, vendored in `wwwroot/lib/three`), coloured by feature (walls, infill, support…), move scrubber, and live follow of the running print
- Terminal with command history and temperature-noise filter
- **Send from your slicer**: OctoPrint-compatible `/api/version` + `/api/files/local` — add a physical printer with host type *OctoPrint* and PrintDeck's API key (Settings page)
- Dark and light themes, mobile layout

## Architecture

Clean Architecture with CQRS through [martinothamar/Mediator](https://github.com/martinothamar/Mediator) (source-generated):

```
src/
  PrintDeck.Domain          entities & rules: PrintJob, PrinterState, temperatures, G-code protocol helpers. No dependencies.
  PrintDeck.Application     Features/<Slice>/{Commands,Queries,Handlers,Dtos} — one type per file;
                            PrinterSession (protocol engine), GcodeAnalyzer, ports (IPrinterTransport, IGcodeFileStore, ISettingsStore)
  PrintDeck.Infrastructure  serial transport, virtual printer, file-system store, JSON settings
  PrintDeck.Web             Blazor UI + slicer API; registers Mediator (generator runs here); Scripts/ = TypeScript (G-code viewer)
tests/                      one test project per layer; folders mirror the project under test
  PrintDeck.Domain.Tests          entities and protocol helpers (pure, no I/O)
  PrintDeck.Application.Tests     handlers per feature slice, G-code analysis/toolpaths, PrinterSession against a
                                  scripted transport; TestDoubles/ holds the fakes
  PrintDeck.Infrastructure.Tests  file/settings storage (temp dirs), virtual printer, end-to-end session against it
  PrintDeck.Web.Tests             slicer + toolpath HTTP endpoints (WebApplicationFactory), formatting, TypeScript checks
```

UI components read through queries and write through commands. `IPrinterEvents` tells components *what* changed (state, temperature, job, …); they re-query, throttled.

## Run

Building needs the .NET SDK and **Node.js/npm**: the viewer's TypeScript (`src/PrintDeck.Web/Scripts`) is compiled to `wwwroot/js` by `dotnet build` (`npm ci` runs automatically the first time). For quick front-end iteration, `npm run watch` in `src/PrintDeck.Web`.


```bash
dotnet run --project src/PrintDeck.Web --urls http://0.0.0.0:5000
```

Ports below 1024 (e.g. 80) need root on Linux and fail with `SocketException (13): Permission denied`. Use a high port, or let the app bind 80 without root:

```bash
sudo setcap 'cap_net_bind_service=+ep' src/PrintDeck.Web/bin/Release/net11.0/PrintDeck.Web
```

Pick **VIRTUAL** as port to try it without a printer. Data (uploads, settings) lives in `~/.printdeck` (`Storage:DataDirectory` to change; `.dev-data/` in Development).

On Linux your user needs serial access: `sudo usermod -aG uucp $USER` (Arch/CachyOS) or `dialout` (Debian/Ubuntu), then log in again.

## Test

```bash
dotnet test
```

One project at a time, e.g. `dotnet test --project tests/PrintDeck.Domain.Tests`. `PrintDeck.Web.Tests` also runs the viewer's TypeScript checks (type-check + [Vitest](https://vitest.dev)) through `TypeScriptTests`, so it needs Node.js/npm. On their own, from `src/PrintDeck.Web`:

```bash
npm test
```
