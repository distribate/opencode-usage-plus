# OpenCode Usage Plus

[![CI](https://github.com/distribate/opencode-usage-plus/actions/workflows/ci.yml/badge.svg)](https://github.com/distribate/opencode-usage-plus/actions/workflows/ci.yml)

A native macOS menu bar app that monitors your
[OpenCode Go](https://opencode.ai/docs/console/go/) subscription usage
(rolling 5-hour, weekly and monthly windows).

The same repository ships a **Windows tray app** (`windows/`) with the same
API, credential discovery and history format. See
[windows/README.md](windows/README.md).

This is a fork of [wiscaksono/opencode-usage](https://github.com/wiscaksono/opencode-usage)
with two additions:

- **zero-config credentials** — uses the OpenCode sign-in token already stored
  in the local OpenCode database, so no API key is required;
- **24-hour history** — a monthly sparkline built from local samples.

![App Screenshot](assets/screenshot.png)

## Features

- **Real-Time Monitoring**: Rolling, Weekly and Monthly usage in the menu bar.
- **Smart Progress Bars**: green / yellow / red depending on usage level.
- **Zero-Config Credentials**: reads the OpenCode OAuth token from
  `~/.local/share/opencode/opencode.db`; an explicit `sk-...` API key can still
  be saved in Settings and takes priority.
- **24h History**: a monthly sparkline from samples taken on every refresh.
- **Auto-Refresh**: every 5 minutes, plus a manual refresh button.
- **Lightweight**: background agent (`LSUIElement`), no dock icon.
- **Terminal-Driven**: plain SwiftPM + Makefile, no `.xcodeproj`.

## Windows tray app

`windows/` contains a .NET 8 / WPF port for Windows 10 and 11:

- tray icon (progress ring, three bars or bolt) and a flyout with the same three
  windows, reset countdowns and 24-hour sparkline;
- reads the same `opencode.db` credential on the Windows data path and keeps the
  same `history.json` shape;
- left click opens the flyout, right click the menu, autostart via `HKCU\...\Run`.

Download `OpenCodeUsagePlus-windows-x64.zip` from
[Releases](https://github.com/distribate/opencode-usage-plus/releases) — it is a
self-contained single executable. Build instructions and limits are in
[windows/README.md](windows/README.md); the shared contract lives in
[docs/port-spec.md](docs/port-spec.md).

## Prerequisites

- macOS 26+
- Xcode 26+ (SwiftUI macros require the full Xcode toolchain; Command Line
  Tools alone are not enough to build)
- OpenCode signed in to OpenCode Console with a Go / Go Plus / Go Teams plan,
  or an `sk-...` API key

## Build & run

```bash
make build
make run
```

The app appears in the menu bar. Releases are produced with `make dmg`.

## Credentials

By default the app uses the OAuth access token that OpenCode stores in
`~/.local/share/opencode/opencode.db` (table `credential`). If you are signed
in to OpenCode, there is nothing to configure.

To override it with a dedicated API key:

1. Open the OpenCode console → **Settings** → **API Keys**.
2. Copy the key (starts with `sk-...`).
3. Open **OpenCode Usage Plus** from the menu bar.
4. Click the gear icon, paste the key, and click **Save**.

The API key wins over the OpenCode token. If neither is available, the setup
screen is shown.

Usage history is stored in
`~/Library/Application Support/OpenCode Usage Plus/history.json`
(percentages and timestamps only, last 24 hours).

## Development

| Command | What it does |
|---|---|
| `make build` | Debug build + `.app` bundle |
| `make run` | Build, kill the old instance, launch |
| `make test` | `swift test` |
| `make format` | `swiftformat .` |
| `make logs` | Stream unified logs for `com.distribate.opencode-usage-plus` |
| `make dmg` | Release build + `.dmg` |

Windows (from `windows/`):

| Command | What it does |
|---|---|
| `dotnet build` | Build the core, WPF shell and tests |
| `dotnet test` | Run the core tests (any OS) |
| `dotnet run --project src/OpenCodeUsagePlus` | Launch the tray app (Windows only) |
| `dotnet publish ... -o publish` | Self-contained single-file exe |

CI runs on `macos-26` (formatting, tests, `make build`, `make dmg`) and on
`windows-latest` (build + tests + publish). Pushing a `v*` tag runs the release
workflow and publishes both the `.dmg` and the Windows `.zip`.

## Releases

Pre-built artifacts are published on every version tag in
[Releases](https://github.com/distribate/opencode-usage-plus/releases):

- `OpenCode Usage Plus.dmg` — macOS;
- `OpenCodeUsagePlus-windows-x64.zip` — Windows 10/11 x64, self-contained.

The macOS app is ad-hoc signed (no paid Developer Program), so Gatekeeper may warn
on first launch: right-click the app → **Open** once.

The Windows build is unsigned as well; SmartScreen may warn — **More info** →
**Run anyway**.

## Credits

- Original app: [wiscaksono/opencode-usage](https://github.com/wiscaksono/opencode-usage)
  by Wisnu Wicaksono, MIT.
- OpenCode Go usage API: `https://opencode.ai/zen/go/v1/usage` (undocumented;
  may change).

## License

MIT. See [LICENSE](LICENSE). The original copyright notice is preserved.
