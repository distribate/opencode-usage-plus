# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

OpenCode Usage Plus — a native macOS menu bar app (SwiftUI) that monitors OpenCode Go subscription usage (rolling, weekly, monthly limits) via `https://opencode.ai/zen/go/v1/usage`.

The repository also ships a Windows 10/11 tray port under `windows/` (.NET 8 + WPF, same API,
credential discovery and history format).

- Pure SwiftPM package. There is no `.xcodeproj` and no xcodegen — do not introduce one.
- Swift 6 with strict concurrency enabled. Deployment target: macOS 26 (requires Xcode 26+).
- Single executable target (`OpencodeUsage`) + test target (`OpencodeUsageTests`).
- The app is a background agent (`LSUIElement`) rendered through `MenuBarExtra` with window style.

## Commands

| Task | Command |
|---|---|
| Build debug + assemble `.app` bundle | `make build` |
| Build & launch | `make run` |
| Run tests | `swift test` |
| Format code | `swiftformat .` |
| Check formatting | `swiftformat --lint .` |
| Clean artifacts | `make clean` |

Before claiming work is done, all three of these must pass:

```bash
swift build && swift test && swiftformat --lint .
```

## Architecture

```
Sources/OpencodeUsage/
  App/           Entry point, MenuBarExtra scene, icon loading
  Views/         ContentView (popup), SettingsView (API key form), SparklineView (history)
  ViewModels/    UsageViewModel — @MainActor ObservableObject, 300s Timer refresh
  Models/        UsageModels.swift — API response types + UsageLevel enum
  Services/      NetworkManager — URLSession fetch + JSON decoding
  Storage/       AppSettings (prefs), KeychainStore (API key), OpenCodeCredentials, UsageHistoryStore
  Utilities/     Duration formatting helpers
Tests/OpencodeUsageTests/
Support/Info.plist   Template; version stamped by Makefile at bundle time
```

Data flow: `MenuBarExtra` → `ContentView` → `UsageViewModel` → `NetworkManager` → opencode API.

Storage split: user preferences live in `UserDefaults` via `AppSettings`; the API key lives in the Keychain via `KeychainStore`. The fallback OAuth token is read read-only from OpenCode's own `opencode.db` on every request and is never persisted. Usage history (percentages only) lives in `~/Library/Application Support/OpenCode Usage Plus/history.json`. Do not move secrets into `UserDefaults`.

The app bundle is assembled manually by the Makefile `bundle` target: binary from `.build/`, plist from `Support/Info.plist`, logo PNG from SwiftPM resources. Version strings are stamped from git tags at bundle time.

## Windows port

```
windows/
  src/OpenCodeUsagePlus.Core/   models, API client, opencode.db access, history, settings (net8.0)
  src/OpenCodeUsagePlus/        WPF shell: NotifyIcon host, flyout, tray icon renderer, autostart
  tests/OpenCodeUsagePlus.Tests xUnit; runs on any OS
  docs/test-checklist.md        manual desktop checks for Windows
  packaging/installer.iss       Inno Setup (per-user)
  tools/make_icon.py            regenerates Resources/app.ico
docs/port-spec.md               shared API / credential / history contract — update it with both apps
```

| Task | Command |
|---|---|
| Build + test | `cd windows && dotnet build && dotnet test` |
| Run (Windows) | `dotnet run --project src/OpenCodeUsagePlus` |
| Self-contained exe | `dotnet publish src/OpenCodeUsagePlus -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o publish` |
| Regenerate icon | `python3 windows/tools/make_icon.py` |

The Windows solution builds and tests on macOS because `EnableWindowsTargeting=true`; the WPF
shell itself only runs on Windows. Do not add WPF/PresentationFramework types to
`OpenCodeUsagePlus.Core` — the core must stay `net8.0` so its tests run everywhere.

Secrets: the API key lives in DPAPI (`%LOCALAPPDATA%\OpenCode Usage Plus\apikey.bin`), the OAuth
token is read read-only from OpenCode's database and never persisted or logged. History is
`%LOCALAPPDATA%\OpenCode Usage Plus\history.json`, same JSON shape as macOS.

## Conventions

- Swift Testing (`import Testing`, `#expect`, `@Test`), not XCTest.
- SwiftFormat config in `.swiftformat`: Swift 6, 4-space indent. Run `swiftformat .` before committing.
- Strict concurrency must stay clean. Pattern for sharing non-Sendable state across `@Sendable` closures: see `SharedFormatters` in `Services/NetworkManager.swift`.
- Prefer boring, simple code. No new dependencies without strong justification.
- Commit messages: lowercase, conventional-style summary line (e.g. `ci: add tag-driven release automation`).

## Apple Documentation (Sosumi)

This repo ships a project skill at `.agents/skills/sosumi/SKILL.md` (plus `skills-lock.json`) for fetching Apple documentation as Markdown.

- Load it whenever a task needs precise Apple API details: SwiftUI/AppKit/Foundation signatures, availability, Human Interface Guidelines, or WWDC transcripts.
- Quick pattern: swap `developer.apple.com` → `sosumi.ai` and fetch — e.g. `https://sosumi.ai/documentation/swiftui/view`.
- Unknown exact symbol path? Search first, then fetch the specific page.
- Full usage guide (patterns, MCP tools, troubleshooting) lives in the SKILL.md itself.

## Gotchas

- **Keychain queries must include `kSecAttrAccount`** (`KeychainStore` pins it to `"api-key"`). The service name collides with items written by older iterations of this app; querying by service alone makes `SecItemUpdate`/`SecItemCopyMatching` act on an arbitrary item and silently corrupt reads/writes. This caused a real production bug.
- The data-protection Keychain probe with legacy fallback in `KeychainStore` is intentional — ad-hoc signed builds can lack DP-keychain entitlements. Do not remove it.
- App version comes only from git tags (`vX.Y.Z`). Untagged builds stamp `0.0.0`. Never hardcode versions in `Support/Info.plist`; never bump versions manually.
- Releases are published exclusively by pushing a tag: `git tag vX.Y.Z && git push origin vX.Y.Z`. The release workflow re-runs lint + tests before building and publishing the dmg.
- The Windows job in the same release workflow runs **after** the macOS job created the release; it uploads `OpenCodeUsagePlus-windows-x64.zip` with `gh release upload --clobber`. Keep that ordering (`needs:`), otherwise the upload races the release creation.
- The Windows exe is signed ad-hoc (not at all). Keep `PublishTrimmed=false`; WPF does not support trimming. Keep `IncludeNativeLibrariesForSelfExtract=true` so `e_sqlite3` works from the single file.
- **Never enable `InvariantGlobalization`** for the Windows projects. WPF resolves `XmlLanguage("en-US")` for every binding and throws "Cannot find non-neutral culture related to 'en-us'" when only the invariant culture exists. It crashes the flyout the moment a binding attaches. Guarded by `GlobalizationTests` and a `runtimeconfig.json` check in CI.
- The flyout must never be destroyed while the app runs: `Closing` is cancelled and becomes `Hide()`. `Show()` on a closed `Window` throws, which used to leave the tray unusable.
- Do not rename `%LOCALAPPDATA%\OpenCode Usage Plus` casually: it holds the DPAPI key, history and log.
- The app is ad-hoc signed on purpose (no paid Developer Program). Do not add entitlements casually — it changes Keychain behavior and Gatekeeper handling.
- CI runs on `macos-26` GitHub runners (lint → test → build). Keep workflows compatible with that image.
