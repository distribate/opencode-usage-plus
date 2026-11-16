# Port spec — shared contract

Everything in this file is what the macOS menu bar app (`Sources/OpencodeUsage`) and the Windows
tray app (`windows/src`) must agree on. Keep both implementations in sync with this document:

## 1. Usage API

```
GET https://opencode.ai/zen/go/v1/usage
Authorization: Bearer <token>
Accept: application/json
x-opencode-org-id: <orgID>        (optional, when the credential carries metadata.orgID)
User-Agent: OpenCodeUsagePlus/<version> (Windows)
```

Response:

```json
{
  "usage": {
    "rolling": { "status": "ok", "percent": 42, "resetsAt": "2026-11-16T20:00:00.000Z" },
    "weekly":  { "status": "ok", "percent": 61, "resetsAt": "2026-11-19T00:00:00.000Z" },
    "monthly": { "status": "rate-limited", "percent": 100, "resetsAt": "2026-12-01T00:00:00.000Z" }
  }
}
```

- `percent` is a number; the API for Go normally sends integers, but a float must not break parsing.
- `resetsAt` is ISO 8601 and may or may not carry fractional seconds.
- `status` is `ok` or `rate-limited`.
- HTTP mapping: `401` → auth expired, `403` → not entitled / Go plan inactive, other non-2xx → request
  failed, unparsable body → parse error.

The endpoint is undocumented upstream; isolate it behind `NetworkManager` (macOS) and
`UsageApiClient` (Windows) so a schema change touches one file per platform.

## 2. Credential storage

### Paths

OpenCode roots its data directory at `$XDG_DATA_HOME` or `<home>/.local/share`, then appends
`opencode` — on **every** platform, Windows included (see `packages/util/src/global-roots.ts`).

| | macOS | Windows |
|---|---|---|
| data dir | `~/.local/share/opencode` | `%USERPROFILE%\.local\share\opencode` |
| database | `opencode.db` | `opencode.db` (`opencode-<channel>.db` on dev channels) |
| override | `OPENCODE_DB` | `OPENCODE_DB` (`:memory:` means "no file") |
| legacy auth | `auth.json` in the data dir | same |
| app data | `~/Library/Application Support/OpenCode Usage Plus` | `%LOCALAPPDATA%\OpenCode Usage Plus` |

Because channel builds use `opencode-<channel>.db`, enumerate `opencode*.db` and prefer the newest
`mtime`; then pick the row with the highest `active`, then `time_created`.

### Database

```sql
select value from credential
where integration_id = 'opencode'
order by coalesce(active, 0) desc, time_created desc, id desc
limit 1
```

`value` is JSON. Union (`packages/schema/src/credential.ts`):

```jsonc
{ "type": "oauth", "access": "...", "refresh": "...", "expires": 1763000000000,
  "metadata": { "orgID": "wrk_...", "orgName": "..." } }
{ "type": "key",   "key": "sk-..." }
{ "type": "external", "metadata": { "orgID": "..." } }   // no local secret, unusable here
```

Legacy `credential` rows and `auth.json` use `{ "opencode": { "access": "...", "refresh": ... } }`
or a bare `{ "access": "..." }`. Parsers must accept: `access`, `key`, `token`; `metadata.orgID`
(case-insensitive `orgID`/`orgId`); nested `opencode` object.

Rules:

- open read-only, `busy_timeout=3000`, retry on `SQLITE_BUSY`;
- if a read-only open fails (WAL + permissions + a running OpenCode), copy `*.db`, `*.db-wal`,
  `*.db-shm` to a temp directory and read the copy, then delete it;
- never write to the database, never log the token, read it fresh before each request
  (OpenCode rotates `access`).

### User override

An explicit `sk-...` key wins over the OpenCode token. Store it in the OS secret store
(DPAPI/Credential Manager on Windows, Keychain on macOS), never in settings JSON.

## 3. History

`history.json` — array of samples, 24 h retention, one sample per refresh, samples closer than 60 s
replace the previous one. macOS writes it under `~/Library/Application Support/OpenCode Usage Plus`,
Windows under `%LOCALAPPDATA%\OpenCode Usage Plus`; the JSON is identical:

```json
[ { "timestamp": "2026-11-16T12:00:00+00:00", "rolling": 42, "weekly": 61, "monthly": 37 } ]
```

- at most 288 samples;
- written atomically (temp file + replace);
- percentages only, no tokens, no model names.

## 4. UI parity

| macOS | Windows |
|---|---|
| `MenuBarExtra` window style | `NotifyIcon` + WPF flyout |
| menu bar label image | 16/24/32 px tray bitmap |
| `NSPopover` | borderless topmost window anchored to the taskbar |
| gear → settings replaces content | same |
| `ProgressView` | custom `UsageBar` element |
| `SparklineView` | custom `Sparkline` element |

Thresholds are shared: `< 51` safe (green), `< 81` elevated (amber), otherwise critical (red).
Notifications (Windows only) fire when the busiest window crosses 80 % and 95 %.

Tray styles: macOS ships `logo` and `badges`; 16 px leaves no room for three pills, so Windows maps
`badges` to a ring (`Ring`) and adds a stacked-bars variant (`Stack`). `Logo` is a bolt tinted by
the worst window's level.

Refresh cadence: 5 minutes, immediately on resume/unlock/network change, and manually.
