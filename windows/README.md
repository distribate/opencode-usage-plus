# OpenCode Usage Plus — Windows tray

A Windows 10/11 companion to the macOS menu bar app: same usage endpoint, same credential
discovery, same history format — but a notification-area icon and flyout instead of a menu bar item.

Download `OpenCodeUsagePlus-windows-x64.zip` from the [releases page](../README.md#releases) and run
`OpenCodeUsagePlus.exe`. The build is self-contained: no .NET runtime required.

| | |
|---|---|
| Windows | 10 1809+ / 11, x64 |
| Runtime | .NET 8, self-contained single file (~140 MB, ~59 MB zipped) |
| Refresh | every 5 minutes, on resume/unlock/network change, manual |
| Tray styles | progress ring (default), three bars, bolt |
| Credentials | OpenCode OAuth token from `opencode.db`; optional `sk-...` key in DPAPI |
| History | 24 h of percentages in `%LOCALAPPDATA%\OpenCode Usage Plus\history.json` |
| Notifications | balloon at 80 % and 95 % of the busiest window |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |

## Build

```powershell
cd windows
dotnet build
dotnet test
dotnet run --project src/OpenCodeUsagePlus
```

Release artifact:

```powershell
dotnet publish src/OpenCodeUsagePlus -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false -p:DebugType=none -o publish
```

The solution also builds on macOS/Linux with `EnableWindowsTargeting` (compile + tests only; the
shell itself needs Windows). `OpenCodeUsagePlus.Core` targets plain `net8.0` so the API, credential
and history tests run on any OS.

## Credentials

Before every request the app resolves a token in this order:

1. an API key saved in Settings (DPAPI-encrypted with `CurrentUser` scope, never plain text);
2. `opencode.db` on the OpenCode data path — `%OPENCODE_DB%` if set, otherwise every
   `opencode*.db` under `%XDG_DATA_HOME%\opencode` (default `%USERPROFILE%\.local\share\opencode`),
   newest first; the `credential` row for `integration_id = 'opencode'` wins by `active`, then
   `time_created`;
3. legacy `%USERPROFILE%\.local\share\opencode\auth.json`.

The database is opened read-only and never modified. If the WAL is locked by a running OpenCode,
the app reads a temporary snapshot instead. Tokens are never logged and never cached on disk.

If the OAuth token expired, start OpenCode once so it can refresh the token, then press Refresh —
or save an `sk-...` key instead. See [../docs/port-spec.md](../docs/port-spec.md) for the full
contract shared with the macOS app.

## Tray behaviour

- **Left click** toggles the flyout, anchored to the taskbar monitor (all four taskbar edges,
  per-monitor DPI aware).
- **Right click** opens the menu: Open usage, Refresh now, Start with Windows, Settings, Open data
  folder, Quit.
- The flyout closes on focus loss or `Esc`.
- Windows 11 hides new tray icons by default — drag the icon out of the `^` overflow once.

## Troubleshooting

Both modes are ordinary command-line flags on the exe:

| Flag | What it does |
|---|---|
| `--diagnose` | Writes `diagnose-<time>.txt` to `%LOCALAPPDATA%\OpenCode Usage Plus` and opens it: version, OS, window mode, settings, database candidates, credential kind/source (never the token), history, one live API call and the last 60 log lines. |
| `--safe-mode` | Renders the flyout as a plain window without transparency, rounded corners or drop shadow. Use it to check whether a crash is compositor/GPU related. Also available as `OPENCODE_USAGE_PLAIN_WINDOW=1`. |

Unhandled errors produce `crash-<time>.txt` next to `log.txt` with the full stack trace and a
message box. Native faults (no managed exception) only leave the log; the last line before the
silence tells where it died. Attach that file — and the Event Viewer entry under Windows Logs →
Application (`.NET Runtime` or `Application Error`) — to a bug report.

## Layout

```
windows/
  src/OpenCodeUsagePlus.Core/   models, API client, credential/database access, history, settings
  src/OpenCodeUsagePlus/        WPF shell: NotifyIcon host, flyout, icon renderer, autostart
  tests/OpenCodeUsagePlus.Tests
  docs/test-checklist.md        manual desktop checklist for Windows
  packaging/installer.iss       Inno Setup (per-user install)
  tools/make_icon.py            regenerates Resources/app.ico
```

## Known limits

- The usage endpoint is undocumented and may change.
- An **active OpenCode Go plan** is required; a 403 is reported as such.
- The build is not code-signed. SmartScreen warns on first launch (`More info` → `Run anyway`).
  Self-signed certificates do not remove the warning; use Azure Trusted Signing or a paid
  certificate for distribution.
- Windows gives no API to pin a tray icon; overflow behaviour is user-controlled.
- WinForms context menus stay light in dark mode — a limitation of WinForms on .NET 8.

## License

MIT. Original macOS app © Wisnu Wicaksono, fork © distribate contributors; see [../LICENSE](../LICENSE).
