# PrintDeck

A modern 3D-printer host in .NET 11 / Blazor Server — an OctoPrint-style web UI for Marlin printers over USB serial.

## Features

- Connect over serial (auto-discovers `/dev/ttyUSB*`, `/dev/ttyACM*`, `/dev/serial/by-id/*`) or the built-in **virtual printer**
- Marlin host protocol: line numbers + checksums, resend handling, `busy:` tolerance, `M155` auto-report with `M105` polling fallback
- Dashboard: job progress with elapsed / remaining / finish time, pause / resume / cancel, live temperatures, presets, history chart, MJPEG camera
- **Progress on the printer's screen** while PrintDeck streams a print: `M73 P<percent> R<minutes left>` and `M117` messages (file name, `Layer 12/80`, paused, done, cancelled). Files that already contain `M73` (PrusaSlicer, OrcaSlicer) keep theirs. Switch it off in Settings
- Control: XY/Z jog with step sizes, homing, extrude/retract (cold-extrusion guard), fan, motors off
- Files: drag-and-drop upload with analysis (print time, filament, layers) from OrcaSlicer, PrusaSlicer and Cura metadata
- **Find files**: search by name (every word must match), sort by newest upload, name, last printed or size, and hide files that already printed successfully. Each file shows how its last print ended; the view is kept in the address, so reloads and bookmarks keep it. Finished prints are recorded per file in `<name>.prints.json` next to the upload
- **History**: every finished print is recorded with its print time (pauses left out) and filament (the printed share for a print that stopped early). The History page shows totals - prints, success rate, printing time, filament - and the recent prints; each file's tooltip shows its own totals
- **File check before printing**: Print first scans the file and lists problems with the line they start on. Errors: binary G-code, unfilled slicer placeholders (`{material_bed_temperature}`), commands that break a streamed print (`M112`, `M110`, `M28`/`M29`, `M997`). Warnings: lines too long for Marlin, extrusion outside the active profile's build volume, extruding before any hotend temperature, a file that never moves. You can print anyway; slicer uploads with `print=true` are refused on errors only
- **SD card**: list the printer's card (long names with `M20 L`), print from it with progress (`M23`/`M24`, `M27`), pause/resume/cancel (`M25`/`M24`/`M524`), copy uploaded files onto it (`M28`/`M29`, 8.3 names), delete, initialise and release. The virtual printer has a card too
- **Firmware dialogs**: follows host action commands, so pausing, resuming or cancelling on the printer's screen keeps PrintDeck in step (`//action:pause`/`paused`/`resume`/`resumed`/`cancel`/`disconnect`). Printer messages and filament runout show as a banner. Host prompts (`M876`, e.g. Marlin's `M600` filament change) appear as buttons on every page, and `busy: paused for user` waits (`M0`/`M1`/`M600`) get a Continue button (`M108`). Answers bypass the command queue, so the printer gets them while it is blocked. SD prints are cancelled with `M524` (Marlin) or `M603` (Prusa firmware)
- G-code viewer: 2D layer view and 3D view (three.js, vendored in `wwwroot/lib/three`), coloured by feature (walls, infill, support…), move scrubber, and live follow of the running print
- **Printer profiles**: save several printers (build volume, extruders, heated bed, jog speeds) and switch in Settings or the connection panel; presets, camera and cancel script stay shared
- Terminal with command history and temperature-noise filter
- **Logs**: switch on a full serial log (every line to and from the printer, timestamped, in `logs/serial.log`); PrintDeck's own log goes to `logs/printdeck.log`. Both roll over by size and download from the Terminal page (admin accounts only)
- **Send from your slicer**: OctoPrint-compatible `/api/version` + `/api/files/local` — add a physical printer with host type *OctoPrint* and PrintDeck's API key (Settings page)
- **App keys** (Settings → Slicer upload): give each slicer or integration its own API key, revoke one without touching the others, and see when each was last used. A key acts as the admin who made it and follows that account's role (a demoted owner's key can look but not upload; deleting the account deletes its keys). Keys are shown once and stored only as a SHA-256 hash. The old shared key keeps working until you turn it off
- **Sign-in** with ASP.NET Core Identity (accounts in SQLite): first-run setup creates the admin, **Admin** and **Viewer** roles (viewers watch, admins control), lockout after 5 wrong passwords, admins reset passwords under Users
- **Login rate limit per address**: after 10 failed sign-ins within 15 minutes from one address (any usernames), that address waits until the oldest failure is 15 minutes old (HTTP 429 with `Retry-After`). IPv6 counts per /64. Paused attempts aren't checked against passwords, so they don't lock accounts either. Tune or switch off (`MaxFailures: 0`) in the `LoginRateLimit` section of `appsettings.json`. Behind a reverse proxy, list the proxy under `ReverseProxy` so each client keeps its own limit (see [Behind a reverse proxy](#behind-a-reverse-proxy))
- **Backup and restore** (Settings → Backup): download one zip with the settings, printer profiles, API key and every uploaded file with its analysis and print history; restore it on this or another PrintDeck. A restore replaces the settings and overwrites files with the same name, other files stay. The whole archive is checked before anything changes, and restoring waits until no print is running. Accounts are not included. Admin accounts only
- **System menu** (Settings → System): restart PrintDeck, reboot or shut down the host, with commands you configure on the host (see below). Admin accounts only, not while printing
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

On first start PrintDeck asks you to create the admin account at `/setup`. Pick **VIRTUAL** as port to try it without a printer. Data lives in `~/.printdeck` (`Storage:DataDirectory` to change; `.dev-data/` in Development): uploads, `settings.json`, `printdeck.db` (accounts), `keys/` (signs login cookies, so restarts keep you signed in) and `logs/`.

Locked out of the only admin account? Stop PrintDeck and delete `printdeck.db`; the next start asks for a new admin (other accounts are lost, settings and files stay).

After changing the Identity model, add a migration (the database upgrades itself on start):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/PrintDeck.Infrastructure --startup-project src/PrintDeck.Web --output-dir Identity/Migrations
```

### Behind a reverse proxy

Behind nginx, Caddy or another reverse proxy, PrintDeck sees every request coming from the proxy. Tell it which proxy to trust, so it takes the client's address from `X-Forwarded-For` (the login rate limit then counts per client instead of pausing everyone at once) and the scheme from `X-Forwarded-Proto`. In the `ReverseProxy` section of `appsettings.json`, or as environment variables such as `ReverseProxy__KnownProxies__0=127.0.0.1`:

```json
"ReverseProxy": {
  "KnownProxies": ["127.0.0.1"],
  "KnownNetworks": []
}
```

`KnownProxies` takes addresses, `KnownNetworks` CIDR ranges such as `172.18.0.0/16` for a proxy in a Docker network. Only the last hop is used: the address the proxy itself saw. With both lists empty (the default) the headers are ignored, so leave them empty when PrintDeck is reached directly; otherwise anyone could pick their own address. A malformed entry stops PrintDeck at start.

nginx on the same machine, with PrintDeck on port 5000 (Blazor needs the WebSocket upgrade; the large body size lets slicers upload):

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 80;
    server_name printdeck.local;
    client_max_body_size 1g;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 1d;
    }
}
```

Caddy sets these headers itself: `reverse_proxy 127.0.0.1:5000`.

### System menu

The restart, reboot and shutdown buttons run shell commands from the `System` section of `appsettings.json` (or environment variables such as `System__RebootCommand`). They are deliberately not editable in the web UI. Buttons without a command stay hidden. For a systemd service called `printdeck`:

```json
"System": {
  "RestartCommand": "sudo systemctl restart printdeck",
  "RebootCommand": "sudo systemctl reboot",
  "ShutdownCommand": "sudo systemctl poweroff"
}
```

Allow exactly those commands without a password, for the user PrintDeck runs as (`sudo visudo -f /etc/sudoers.d/printdeck`):

```
printdeck ALL=(root) NOPASSWD: /usr/bin/systemctl restart printdeck, /usr/bin/systemctl reboot, /usr/bin/systemctl poweroff
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
