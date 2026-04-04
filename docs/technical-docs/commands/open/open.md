# OpenCommand

**File:** `src/apix/Commands/Open/OpenCommand.cs`  
**Registered as:** `apix open` (default command on the `open` branch)  
**Base class:** `AsyncCommand<OpenCommand.Settings>`  
**DI:** `IHttpClientFactory` (constructor injection)

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Url` | `string?` | `-u\|--url` | Required (runtime check). Full URL including query params |
| `Method` | `string` | `-x\|--method` | HTTP method. Default: `"GET"` |
| `Verbose` | `bool` | `-v\|--verbose` | Print full headers and body |
| `NoSave` | `bool` | `--no-save` | Skip saving to history |

`Url` is declared as an **option** (not a positional argument) because Spectre.Console.Cli treats unrecognised positional tokens inside a branch as unknown sub-command names — making the URL unreachable as a positional arg. Using `--url` avoids this routing ambiguity.

`HistoryKey = "_open"` is a public constant used by all three Open commands to reference the global open-request history file (`~/.apix/history/_open.json`).

---

## Execution flow

```
1. Validate --url: non-empty, absolute URI, http/https scheme
2. Determine hasBody: method is POST, PUT, or PATCH
3. Build editor template:
   { "headers": {} }               ← GET/DELETE/HEAD
   { "headers": {}, "body": {} }   ← POST/PUT/PATCH
4. EditorService.OpenForEditAsync(template)
5. RequestHelpers.TryParseInputs(editedJson)
   → headerParams, requestBody (ignores path/query — URL is literal)
6. Build HttpRequestMessage(method, url)
   → apply headers, attach body as application/json if present
7. AnsiConsole.Status → httpClient.SendAsync
8. Print REQUEST + RESPONSE blocks
9. HistoryService.AppendAsync(HistoryKey, entry)  → unless --no-save
10. Print footer with [apix open history N] / [apix open replay N] hints
```

---

## Key differences from `CallCommand`

| | `call` | `open` |
|---|---|---|
| Service required | Yes | No |
| Schema required | Yes | No |
| URL construction | `BuildUrl(baseUrl, pathPattern, ...)` | Literal `settings.Url` |
| Editor gating | Only if template is non-null | Always |
| Template sections | path / query / headers / body | headers / body only |
| OperationId in history | set to operationId | `null` |
| History key | service name | `"_open"` |

---

## History entry

Saved with `OperationId: null` since open requests are schema-free. The `HistoryEntry.OperationId` field is `string?` to accommodate this.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| `--url` not provided | `✕ URL is required` | 1 |
| Invalid / non-http URL | `✕ Invalid URL: ...` | 1 |
| Editor cancelled | `⚠ Cancelled` | 1 |
| Invalid JSON in editor | `✕ Invalid JSON in editor: ...` | 1 |
| Network failure | `✕ Could not connect to ...` | 1 |
