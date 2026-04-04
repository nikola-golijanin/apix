# HistoryCommand

**File:** `src/apix/Commands/HistoryCommand.cs`  
**Registered as:** `apix history`  
**Base class:** `AsyncCommand<HistoryCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Service` | `string` | arg 0 `<service>` | Required. Registered service name |
| `Id` | `int?` | arg 1 `[id]` | Optional. Entry ID — presence switches list→inspect |
| `Verbose` | `bool` | `-v\|--verbose` | Show headers + full body (inspect mode) |
| `RequestOnly` | `bool` | `--request-only` | Print only request block (inspect mode) |
| `ResponseOnly` | `bool` | `--response-only` | Print only response block (inspect mode) |
| `Curl` | `bool` | `-c\|--curl` | Print request as curl command (inspect mode) |
| `TakeAll` | `bool` | `-a\|--all` | Show all entries instead of last 20 (list mode) |
| `Quiet` | `bool` | `-q\|--quiet` | Tab-separated raw output for piping (list mode) |

---

## Dispatch

`ExecuteAsync` resolves the service then delegates:

- `Id is null` → `ListAsync`
- `Id has value` → `InspectAsync`

---

## List mode (`ListAsync`)

1. `HistoryService.LoadAllAsync(service)` → all entries
2. Order by `Id` descending, take 20 (or all with `--all`)
3. Compute column widths dynamically:
   - Fixed columns: `#`, Method, Status, Duration, When
   - Variable columns: Path, Operation — capped so the total row never exceeds terminal width
   - `pathMax = remaining * 3/5`, `opMax = remaining * 2/5`
4. Print table with Spectre markup

**Path display:** `StripBase(url, service.BaseUrl)` removes the base URL prefix so only the path segment is shown (e.g., `/orders/abc-123` instead of the full URL).

**Quiet mode:** Writes tab-separated `#{id}\t{method}\t{url}\t{statusCode}\t{operationId}` to `Console.WriteLine` (bypasses Spectre markup for pipe-friendliness).

---

## Inspect mode (`InspectAsync`)

1. `HistoryService.FindAsync(service, id)` → single `HistoryEntry`
2. If `--curl`: call `PrintCurl(e)` and return
3. Otherwise print:
   - Entry header: `#id — datetime`
   - REQUEST block (unless `--response-only`): method + URL, optional verbose headers, optional body
   - RESPONSE block (unless `--request-only`): status + duration, optional verbose headers, optional body

Body is always truncated in non-verbose mode via `OutputHelpers.PrintBody(body, full: settings.Verbose)`.

---

## Curl export (`PrintCurl`)

Builds a multiline `curl` command:

```csharp
sb.Append($"curl -X {e.Method} {e.Url}");
foreach (var (k, v) in e.RequestHeaders)
    sb.Append($" \\\n  -H \"{k}: {v}\"");
if (e.RequestBody is not null)
    sb.Append($" \\\n  -d '{e.RequestBody.ReplaceLineEndings(" ")}'");
```

The body is compacted to a single line by replacing newlines with spaces.

---

## Private helpers

| Helper | Purpose |
|---|---|
| `StatusColor(int)` | Maps HTTP status ranges to Spectre color names (`green`/`cyan`/`yellow`/`red`) |
| `Trunc(string, int)` | Truncates to max characters, appends `…` |
| `StripBase(string, string)` | Removes `baseUrl` prefix from full URL for display |

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found` + fuzzy suggestion | 1 |
| Entry ID not found | `✕ Entry #N not found in <service> history` | 1 |
