# PrintDeck

A modern 3D-printer host in .NET 11 / Blazor Server — an OctoPrint-style web UI for Marlin printers over USB serial.

## Features

- Connect over serial (auto-discovers `/dev/ttyUSB*`, `/dev/ttyACM*`, `/dev/serial/by-id/*`) or the built-in **virtual printer**
- Marlin host protocol: line numbers + checksums, resend handling, `busy:` tolerance, `M155` auto-report with `M105` polling fallback
- Dashboard: job progress with elapsed / remaining / finish time, pause / resume / cancel, live temperatures, presets, history chart, MJPEG camera
- **Progress on the printer's screen** while PrintDeck streams a print: `M73 P<percent> R<minutes left>` and `M117` messages (file name, `Layer 12/80`, paused, done, cancelled). Files that already contain `M73` (PrusaSlicer, OrcaSlicer) keep theirs. Switch it off in Settings
- Control: XY/Z jog with step sizes, homing, extrude/retract (cold-extrusion guard), fan, motors off
- Files: drag-and-drop upload with analysis (print time, filament, layers) from OrcaSlicer, PrusaSlicer and Cura metadata
- **SD card**: list the printer's card (long names with `M20 L`), print from it with progress (`M23`/`M24`, `M27`), pause/resume/cancel (`M25`/`M24`/`M524`), copy uploaded files onto it (`M28`/`M29`, 8.3 names), delete, initialise and release. The virtual printer has a card too
- **Firmware dialogs**: follows host action commands, so pausing, resuming or cancelling on the printer's screen keeps PrintDeck in step (`//action:pause`/`paused`/`resume`/`resumed`/`cancel`/`disconnect`). Printer messages and filament runout show as a banner. Host prompts (`M876`, e.g. Marlin's `M600` filament change) appear as buttons on every page, and `busy: paused for user` waits (`M0`/`M1`/`M600`) get a Continue button (`M108`). Answers bypass the command queue, so the printer gets them while it is blocked. SD prints are cancelled with `M524` (Marlin) or `M603` (Prusa firmware)
- G-code viewer: 2D layer view and 3D view (three.js, vendored in `wwwroot/lib/three`), coloured by feature (walls, infill, support…), move scrubber, and live follow of the running print
- **Printer profiles**: save several printers (build volume, extruders, heated bed, jog speeds) and switch in Settings or the connection panel; presets, camera and cancel script stay shared
- Terminal with command history and temperature-noise filter
- **Send from your slicer**: OctoPrint-compatible `/api/version` + `/api/files/local` — add a physical printer with host type *OctoPrint* and PrintDeck's API key (Settings page)
- **Sign-in** with ASP.NET Core Identity (accounts in SQLite): first-run setup creates the admin, **Admin** and **Viewer** roles (viewers watch, admins control), lockout after 5 wrong passwords, admins reset passwords under Users
- Dark and light themes, mobile layout

## Architecture

Clean Architecture with CQRS through [martinothamar/Mediator](https://github.com/martinothamar/Mediator) (source-generated):

```
src/
  PrintDeck.Domain          entities & rules: PrintJob, PrinterState, temperatures, G-code protocol helpers. No dependencies.
  PrintDeck.Application     Features/<Slice>/{Commands,Queries,Handlers,Dtos} — one type per file;
                            PrinterSession (protocol engine), GcodeAnalyzer, ports (IPrinterTransport, IGcodeFileStore, ISettingsStore)
  PrintDeck.Infrastructure  serial transport, virtual printer, file-system store, JSON settings,
                            Identity on SQLite (Identity/, EF Core migrations in Identity/Migrations)
  PrintDeck.Web             Blazor UI + slicer API; registers Mediator (generator runs here); Scripts/ = TypeScript (G-code viewer)
tests/                      one test project per layer; folders mirror the project under test
  PrintDeck.Domain.Tests          entities and protocol helpers (pure, no I/O)
  PrintDeck.Application.Tests     handlers per feature slice, G-code analysis/toolpaths, PrinterSession against a
                                  scripted transport; TestDoubles/ holds the fakes
  PrintDeck.Infrastructure.Tests  file/settings storage (temp dirs), virtual printer, end-to-end session against it
  PrintDeck.Web.Tests             slicer + toolpath HTTP endpoints (WebApplicationFactory), formatting, TypeScript checks
```

UI components read through queries and write through commands. `IPrinterEvents` tells components *what* changed (state, temperature, job, …); they re-query, throttled.

Every command and query passes `AuthorizationBehavior`: queries need a signed-in user, commands need an admin unless the message is marked `IAllowAnonymous` (setup), `IAllowAnyUser` (own password) or `IAccountAdministration` (user management: a real admin account, not the slicer API key). The slicer API key is its own authentication scheme and acts as an admin for uploads.

## Run

Building needs the .NET SDK and **Node.js/npm**: the viewer's TypeScript (`src/PrintDeck.Web/Scripts`) is compiled to `wwwroot/js` by `dotnet build` (`npm ci` runs automatically the first time). For quick front-end iteration, `npm run watch` in `src/PrintDeck.Web`.


```bash
dotnet run --project src/PrintDeck.Web --urls http://0.0.0.0:5000
```

Ports below 1024 (e.g. 80) need root on Linux and fail with `SocketException (13): Permission denied`. Use a high port, or let the app bind 80 without root:

```bash
sudo setcap 'cap_net_bind_service=+ep' src/PrintDeck.Web/bin/Release/net11.0/PrintDeck.Web
```

On first start PrintDeck asks you to create the admin account at `/setup`. Pick **VIRTUAL** as port to try it without a printer. Data lives in `~/.printdeck` (`Storage:DataDirectory` to change; `.dev-data/` in Development): uploads, `settings.json`, `printdeck.db` (accounts) and `keys/` (signs login cookies, so restarts keep you signed in).

Locked out of the only admin account? Stop PrintDeck and delete `printdeck.db`; the next start asks for a new admin (other accounts are lost, settings and files stay).

After changing the Identity model, add a migration (the database upgrades itself on start):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/PrintDeck.Infrastructure --startup-project src/PrintDeck.Web --output-dir Identity/Migrations
```

On Linux your user needs serial access: `sudo usermod -aG uucp $USER` (Arch/CachyOS) or `dialout` (Debian/Ubuntu), then log in again.

## Test

```bash
dotnet test
```

One project at a time, e.g. `dotnet test --project tests/PrintDeck.Domain.Tests`. `PrintDeck.Web.Tests` also runs the viewer's TypeScript checks (type-check + [Vitest](https://vitest.dev)) through `TypeScriptTests`, so it needs Node.js/npm. On their own, from `src/PrintDeck.Web`:

```bash
npm test
```
