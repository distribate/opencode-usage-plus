# Manual test checklist (Windows)

The core is covered by `dotnet test`; the shell needs a real desktop. Run through this list before
tagging a release.

## Build

```powershell
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/OpenCodeUsagePlus
# or
dotnet publish src/OpenCodeUsagePlus -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false -o publish
```

## Tray

- [ ] Icon appears in the tray (Windows 11: drag it out of the `^` overflow once).
- [ ] Right click opens the menu with correct checkmark state for "Start with Windows".
- [ ] Left click opens the flyout above the taskbar; second left click closes it.
- [ ] Clicking elsewhere closes the flyout; `Esc` closes it.
- [ ] Taskbar moved to top / left / right: the flyout stays inside the work area with an 8 px gap.
- [ ] Display scale 100 %, 150 %, 200 %, mixed-DPI dual monitor: icon is crisp, flyout is not clipped.
- [ ] Theme light ↔ dark: flyout colours follow; tray icon switches track colour.
- [ ] Explorer restart (`taskkill /f /im explorer.exe`, `start explorer`): icon comes back.

## Data

- [ ] Signed in to OpenCode, no saved key: status says "Using the OpenCode sign-in token", numbers match the console.
- [ ] Save a bad `sk-...` key: 401 is reported as "Authentication expired".
- [ ] Save a valid key: it wins, `%LOCALAPPDATA%\OpenCode Usage Plus\apikey.bin` exists and is not readable as text.
- [ ] Clear the key: falls back to the OpenCode token.
- [ ] An account without a Go plan: 403 message is shown, not a generic failure.
- [ ] Pull the network cable: "Network error", previous numbers stay with a warning.
- [ ] `history.json` grows and survives a restart; sparkline draws after ~2 samples.

## Lifecycle

- [ ] "Start with Windows" writes `HKCU\...\Run\OpenCodeUsagePlus` and survives a reboot.
- [ ] Sleep/resume refreshes (see `log.txt`).
- [ ] Lock/unlock refreshes.
- [ ] Second launch focuses the existing instance and exits (no duplicate icon).
- [ ] Quit removes the icon immediately.
- [ ] Balloon appears once at 80 % and once at 95 % of the busiest window.
- [ ] `log.txt` never contains a token (search for `sk-` and `at-`).

## Packaging

- [ ] `OpenCodeUsagePlus.exe` starts on a clean Windows 10 1809 VM without .NET installed.
- [ ] Inno Setup installer installs per-user, creates a Start Menu shortcut, uninstalls cleanly.
- [ ] SmartScreen warning is documented in the README (unsigned build).
