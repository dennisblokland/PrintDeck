# Roadmap

Features PrintDeck still lacks compared to an OctoPrint 1.11 setup (Prusa MK3S over USB, with PSU Control, OctoLight, MQTT, Home Assistant, Slicer Thumbnails, Camera Settings, Firmware Updater and Connect And Print plugins). Picked 2026-11-14.

Done: login and user accounts (ASP.NET Core Identity on SQLite, Admin and Viewer roles); multiple printer profiles; SD card.

## Wanted

### 1. Printer connection

Mostly in `PrinterSession` and the Control page.

- [ ] **Auto-connect on startup.** Connect to the last port and baud rate when PrintDeck starts; retry when the port appears.
- [ ] **Firmware dialogs and action commands.** Handle `// action:pause`, `resume`, `cancel`, `M876` host prompts, filament runout and `M600` filament change dialogs, so host and printer agree on job state when you pause from the LCD.
- [ ] **Progress on the printer screen.** Send `M73` progress and `M117` messages during a job.
- [ ] **Feed-rate and flow sliders.** `M220` / `M221` during a print.
- [ ] **G-code scripts.** Before/after print, on pause, on resume, plus reusable snippets (`disable_hotends`, `disable_bed`). Replaces the single `CancelGcode` setting.
- [ ] **Keyboard jogging.** Arrow keys and Page Up/Down on the Control page.

### 2. Files

- [ ] **Slicer thumbnails.** Read the embedded PNG/QOI thumbnails from OrcaSlicer / PrusaSlicer G-code; show them in the file list and job card.
- [ ] **Folders.** Create, rename, move; upload into a folder (also via the slicer API `path` field).
- [ ] **Sort, search and filter.** By name, upload date, last print, size; hide files that printed successfully.
- [ ] **Print history and statistics.** Per-file success/fail count, last print date, total print time and filament.
- [ ] **File problem check.** Warn about known bad G-code before a print starts.
- [ ] **Start print after upload.** Slicer upload with `print=true` connects first if needed.
- [x] **SD card.** List (`M20`), select and print (`M23`/`M24`), upload, init and release.

### 3. Access

- [ ] **Per-app API keys.** Separate, revocable keys per slicer or integration instead of one shared key. Builds on the user accounts.
- [ ] **Login rate limit per IP.** Identity locks an account after 5 wrong passwords; a per-IP limit would also slow guessing across many usernames.

### 4. System

- [x] **Multiple printer profiles.** Switch between saved profiles (bed size, extruders, heated bed).
- [ ] **Editable terminal filters.** Choose which lines to hide instead of one fixed temperature filter.
- [ ] **Serial log and app logs.** Toggle a full serial log; download log files.
- [ ] **Health checks.** Low disk space, undervoltage on a Pi, known-bad firmware versions.
- [ ] **System menu.** Restart PrintDeck, reboot or shut down the host.
- [ ] **Backup and restore.** Settings, profiles and files as one archive.

### 5. Camera

- [ ] **Snapshot URL, rotate and flip.** Still image for notifications; stream orientation options.
- [ ] **Camera settings.** Brightness, focus, exposure and other v4l2 controls.
- [ ] **Timelapse.** Per-layer or timed snapshots, rendered to a video after the print.

## Maybe

- [ ] **Plugin or module system.** A way to add the smart-home features below without growing the core.
  - [ ] Printer power control through Home Assistant, with auto power-off when idle and cooled down.
  - [ ] Printer light through Home Assistant.
  - [ ] MQTT events (state, temperatures, job progress).
  - [ ] Home Assistant discovery.
  - [ ] Event hooks: run a command or webhook on PrintDone, PrintFailed, Disconnected.
- [ ] **Custom control buttons.** User-defined buttons that send a G-code sequence.
- [ ] **Firmware flashing.** Upload a `.hex` and flash the printer from the browser.
- [ ] **Self-update.** Much later.
