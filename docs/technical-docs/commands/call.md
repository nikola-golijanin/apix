# CallCommand

**File:** `src/apix/Commands/CallCommand.cs`  
**Registered as:** `apix call`  
**Base class:** `AsyncCommand<CallCommand.Settings>`  
**DI:** `IHttpClientFactory` (constructor injection)

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Service` | `string` | arg 0 `<service>` | Required. Registered service name |
| `OperationId` | `string` | arg 1 `<operationId>` | Required. Operation ID from the OpenAPI schema |
| `Verbose` | `bool` | `-v\|--verbose` | Print request/response headers and full body |
| `NoSave` | `bool` | `--no-save` | Skip saving to history |

---

## Execution flow

```
1. ServiceRegistry.FindAsync(service)         → resolve ServiceEntry
2. ServiceRegistry.OpenSchema(service)        → open schema stream
   OpenApiJsonReader.ReadAsync(stream)         → parse OpenApiDocument
3. FindOperation(doc, operationId)            → locate path pattern + HttpMethod + OpenApiOperation
4. EditorService.GenerateTemplate(operation)  → build JSON input template (null = no input needed)
   EditorService.OpenForEditAsync(template)   → open editor, wait, return edited JSON
   RequestHelpers.TryParseInputs(json)        → extract path/query/header/body dicts
5. RequestHelpers.BuildUrl(baseUrl, pattern,  → construct final URL
              pathParams, queryParams)
6. Build HttpRequestMessage
   → add headers from headerParams
   → attach StringContent body (application/json) if body present
7. AnsiConsole.Status → httpClient.SendAsync
8. Print REQUEST block (always), RESPONSE block
9. HistoryService.AppendAsync("service", entry)  → unless --no-save
10. Print footer with history/replay hints
```

---

## Operation lookup (`FindOperation`)

Iterates `doc.Paths` → each path's `Operations` dictionary. Compares `op.OperationId` case-insensitively. Returns `(pathPattern, HttpMethod, OpenApiOperation)?`.

`ListOperationIds` is a companion helper that collects all non-empty operation IDs for the "did you mean?" suggestion.

---

## Editor gating

The editor is **only opened if `GenerateTemplate` returns non-null**. Operations with no path params, query params, header params, and no JSON body skip the editor entirely and execute immediately. See [`EditorService`](../services/editor-service.md) for template generation details.

---

## HTTP request construction

```csharp
using var request = new HttpRequestMessage(method, url);

foreach (var (k, v) in headerParams)
    request.Headers.TryAddWithoutValidation(k, v);

if (requestBody is not null)
    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
```

`TryAddWithoutValidation` is used to avoid Spectre silently dropping headers that have non-standard characters or values.

---

## Output formatting

`sepWidth` is computed as `max(40, "► METHOD URL".Length)` so the separator line always spans the full URL.

- `Verbose = false`: prints method + URL in request block; response body truncated to 40 lines via `OutputHelpers.PrintBody(body, full: false)`.
- `Verbose = true`: prints headers under both blocks; full response body.

Truncation appends `... lines truncated — rerun with --verbose to print in full`.

---

## History entry

Saved as `HistoryEntry` via `HistoryService.AppendAsync(settings.Service, entry)`:

- `OperationId` = `settings.OperationId`
- `RequestHeaders` = accumulated dict (including `Content-Type` if body was set)
- `ResponseHeaders` = flattened from `response.Headers` + `response.Content.Headers`

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found` + fuzzy suggestion | 1 |
| Schema parse failure | `✕ Failed to read stored schema` | 1 |
| Operation not found | `✕ Operation not found` + suggestion | 1 |
| Editor cancelled | `⚠ Cancelled` | 1 |
| Invalid JSON in editor | `✕ Invalid JSON in editor: ...` | 1 |
| Network failure | `✕ Could not connect to ...` | 1 |
