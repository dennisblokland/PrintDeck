# PrintDeck

OctoPrint-style 3D-printer host in .NET 11 / Blazor Server, for Marlin printers over USB serial. The owner prints on a Prusa MK3S (Prusa firmware, Marlin-based but not identical). See `README.md` for features and the open GitHub issues labeled `enhancement` for what is wanted next.

## Build and test

```bash
dotnet build
dotnet test                                               # all five test projects
dotnet test --project tests/PrintDeck.Application.Tests   # one project
cd src/PrintDeck.Web && npm test                          # viewer TypeScript: tsc + vitest
dotnet run --project src/PrintDeck.Web --urls http://0.0.0.0:5000
```

- Building needs Node/npm: `dotnet build` compiles `src/PrintDeck.Web/Scripts` (TypeScript) to `wwwroot/js` and runs `npm ci` the first time.
- CI (`.github/workflows/ci.yml`) runs `npm test`, `dotnet build -c Release` and `dotnet test -c Release --no-build`.
- First start asks for an admin at `/setup`. Pick port **VIRTUAL** to use the in-process virtual printer. Development data (accounts, settings, uploads) lives in `.dev-data/` at the repo root; delete it to start over.

### Cloud sessions without the .NET 11 SDK

Claude's cloud containers may only offer `dotnet-sdk-10.0` (apt) and cannot download .NET 11. To check work anyway, build a throwaway copy outside the repo, retargeted to .NET 10. Never commit these changes:
- `net11.0` → `net10.0` in every `*.csproj` and `Directory.Build.props`
- `Version="11.0.0"` → `10.0.0` in `Directory.Packages.props`
- add `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" VersionOverride="10.0.0" />` to `PrintDeck.Application.csproj`
- delete the `LastMigrationId` override in `Identity/Migrations/PrintDeckDbContextModelSnapshot.cs`

Under .NET 10, `LoginFlowTests.Logout_needs_the_antiforgery_token_and_then_signs_out` fails; that is the runtime, not a regression. Say in the PR that tests ran on .NET 10.

## Architecture

Clean Architecture with CQRS through source-generated Mediator (`martinothamar/Mediator`).

- `PrintDeck.Domain`: entities and pure rules (`PrintJob`, `PrinterState`, protocol parsers in `Protocol/`, `SdCard/`, `Settings/HostSettings`). No dependencies, no I/O.
- `PrintDeck.Application`:
  - `Printer/PrinterSession.cs` is the protocol engine. It is a singleton: one command in flight, line numbers and checksums, resends, temperature polling, job streaming, the SD card, and host action commands and prompts.
  - Feature slices live in `Features/<Slice>/{Commands,Queries,Handlers,Dtos}`.
  - Ports live in `Abstractions/Interfaces`.
- `PrintDeck.Infrastructure`: serial transport, the virtual printer (`Virtual/VirtualPrinterTransport*.cs`, a Marlin imitation with heaters, SD card and M600 prompts), file and JSON storage, and Identity on SQLite with EF migrations in `Identity/Migrations`.
- `PrintDeck.Plugins.Abstractions`: the module contract (`IPrintDeckModule`, `IPluginContext`). No references to the rest of PrintDeck.
- `PrintDeck.Modules.*`: in-tree modules (Home Assistant light). They reference only the contract (`ModuleBoundaryTests`); if a module needs more, `IPluginContext` grows (implemented by `Application/Plugins/PluginContext` on top of Mediator). Modules are listed in `Program.cs` and switched on in Settings → Modules; their UI text lives in their own `Resources/`.
- `PrintDeck.Web`:
  - Blazor UI. Components inherit `Components/Shared/PrinterComponent`, read through queries, write through commands, and re-query on `IPrinterEvents` changes.
  - The OctoPrint-compatible slicer API lives in `Api/`.

`AuthorizationBehavior` runs on every message:
- Queries need a signed-in user.
- Commands need an admin, unless the message is marked `IAllowAnonymous`, `IAllowAnyUser` or `IAccountAdministration`.
- In the UI, hide controls from viewers with `CanControl`.

## Conventions

- One type per file. Enums go in an `Enums/` folder next to the code that uses them.
- Tabs, file-scoped namespaces, explicit types rather than `var` (see `.editorconfig`). Use primary constructors where they fit.
- Line endings: `*.cs` and `*.razor` are CRLF, everything else LF (`.editorconfig`). There is no `.gitattributes`, so scripted edits must keep the existing endings: in Python, read and write with `newline=''`, and give new C#/Razor files CRLF.
- UI text goes in resource files, never in markup:
  - A component injects `IStringLocalizer<ItsType> L` and uses `@L["Key"]`. Its text lives in `Resources/<same path as the component>.resx`.
  - Words many pages share (Save, Cancel, Delete, Download…) go in `Resources/SharedText.resx`, used as `Shared["Key"]` (`PrinterComponent` injects it).
  - Placeholders use `{0}`: `L["Deleted", name]`. Plurals get two keys.
  - Text with markup goes in a key ending in `Html` and is rendered with `L.Html("KeyHtml", args)`, which HTML-encodes the args.
  - `ResourceKeyTests` fails on missing or unused keys and on misplaced markup.
- Results, not exceptions, for expected failures: `Result.Failure("Printer is not connected.")`, `Result<T>`. Error messages are short user-facing sentences; tests assert on them.
- `PrinterSession` changes:
  - Take `_gate` for job and state changes.
  - Publish a `PrinterChange` flag after each change.
  - Add lines to the terminal through `Log`.
  - Commands go through `Enqueue`. Only emergency-parser commands (M108/M876) go out of band (`SendOutOfBand`).
- When adding a member to `IPrinterSession`, also update `tests/PrintDeck.Application.Tests/TestDoubles/FakePrinterSession.cs`.
- Firmware differences belong in the session. For example, SD abort is `M603` on Prusa firmware and `M524` on Marlin.
- Teach the virtual printer any new protocol feature, so it can be tried without hardware and tested end to end.

## Tests

xUnit v3 on Microsoft.Testing.Platform. Each test project mirrors the folders of the project it tests.

- Domain: pure unit tests.
- Application:
  - Handler tests use the fakes in `TestDoubles/`.
  - Session tests run against `ScriptedTransport`, which answers each written line through `Respond` and pushes unsolicited lines with `Emit`. Use `Eventually.Until(...)` instead of sleeps.
  - The scripted printer answers the moment a line is written. Wait for the last connect command (`M20 L`) before emitting lines whose order matters.
- Infrastructure: `VirtualPrinterTransportTests` talks to the virtual printer directly. `PrinterSessionWithVirtualPrinterTests` runs the real session against it.
- Web: `WebApplicationFactory` endpoint tests, plus the TypeScript checks.
- Modules: each module's tests drive it through a fake `IPluginContext`; they reference only the module.

## Roadmap workflow

The roadmap lives in GitHub issues (`gh issue list --label enhancement`). Each issue has an `area:` label; issues labeled `maybe` are not committed, so skip them unless asked. Pick an open issue and implement it across the layers, with tests and a virtual-printer path. Add a feature line to `README.md`. Open a PR against `main` that says `Closes #<issue>` and describes what changed, how it was tested, and anything that still needs checking on the real MK3S.
