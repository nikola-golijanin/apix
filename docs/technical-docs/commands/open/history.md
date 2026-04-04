# OpenHistoryCommand

**File:** `src/apix/Commands/Open/OpenHistoryCommand.cs`  
**Registered as:** `apix open history`  
**Base class:** `AsyncCommand<OpenHistoryCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Id` | `int?` | arg 0 `[id]` | Optional. Presence switches list→inspect mode |
| `Verbose` | `bool` | `-v\|--verbose` | Show headers + full body (inspect mode) |
| `RequestOnly` | `bool` | `--request-only` | Print only request block |
| `ResponseOnly` | `bool` | `--response-only` | Print only response block |
| `Curl` | `bool` | `-c\|--curl` | Print request as curl command |
| `TakeAll` | `bool` | `-a\|--all` | Show all entries (list mode, default: last 20) |

No `Service` argument — all open history lives under `OpenCommand.HistoryKey` (`"_open"`).

---

## Differences from `HistoryCommand`

| | `history <service>` | `open history` |
|---|---|---|
| Service argument | Required | None (always `"_open"`) |
| Path column | `StripBase(url, baseUrl)` | Full URL (no base to strip) |
| Operation column | Present | Absent |
| Quiet mode | Present | Absent |

---

## List mode

Same structure as `HistoryCommand.ListAsync` with one column substitution: the Path/Operation columns are replaced with a single URL column. URL width is capped at the available terminal width after fixed-width columns are accounted for.

Long URLs are truncated with `Trunc(url, urlWidth)` + `…` suffix.

---

## Inspect mode

Identical to `HistoryCommand.InspectAsync` without the service-scoped lookup — calls `HistoryService.FindAsync(OpenCommand.HistoryKey, id)`.

---

## Curl export

Identical implementation to `HistoryCommand.PrintCurl`.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Entry ID not found | `✕ No open request found with id #N` | 1 |
