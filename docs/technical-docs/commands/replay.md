# ReplayCommand

**File:** `src/apix/Commands/ReplayCommand.cs`  
**Registered as:** `apix replay`  
**Base class:** `AsyncCommand<ReplayCommand.Settings>`  
**DI:** `IHttpClientFactory` (constructor injection)

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Service` | `string` | arg 0 `<service>` | Required. Registered service name |
| `Id` | `int` | arg 1 `<id>` | Required. History entry to replay |
| `Edit` | `bool` | `-e\|--edit` | Open editor pre-filled with stored values |
| `NoSave` | `bool` | `--no-save` | Skip saving the new entry to history |
| `Verbose` | `bool` | `-v\|--verbose` | Print full headers and body |

---

## Execution flow

```
1. ServiceRegistry.FindAsync(service)
2. HistoryService.FindAsync(service, id)

3a. if --edit:
    ├─ try: load schema → FindOperation → GenerateTemplate → PreFillTemplate
    └─ fallback: BuildFallbackTemplate (from stored values)
    OpenEditor → TryParseInputs → build url + headers + body
    RequestHelpers.BuildUrl(baseUrl, effectivePath, pathParams, queryParams)

3b. if no --edit:
    use historyEntry.Url, historyEntry.RequestBody, historyEntry.RequestHeaders directly

4. Build HttpRequestMessage → execute → print → HistoryService.AppendAsync
```

---

## Edit mode: template pre-filling (`PreFillTemplate`)

When `--edit` is specified and a schema is available, the flow is:

1. Load and parse the stored OpenAPI schema
2. Find the original operation by `historyEntry.OperationId`
3. Call `EditorService.GenerateTemplate(operation)` to get a fresh schema-typed template
4. Call `PreFillTemplate(template, historyEntry, pathPattern)` to populate values

**Path params** are extracted by converting the OpenAPI path pattern into a named-capture regex:

```csharp
// Pattern: /orders/{orderId}
// Regex:   ^/orders/(?<orderId>[^/?]+)$
var regexPattern = Regex.Replace(
    Regex.Escape(pathPattern),
    @"\\{(\w+)\\}",
    m => $"(?<{m.Groups[1].Value}>[^/?]+)");
```

The stored URL's `AbsolutePath` is matched against this regex; captured group values are URI-unescaped and inserted into the template.

**Query params** are parsed from the stored URL using `HttpUtility.ParseQueryString`.

**Headers** (excluding `Content-Type`) and **body** are copied from the stored entry. The body is parsed as `JsonNode` and deep-cloned to avoid mutating the stored entry.

---

## Edit mode: fallback template (`BuildFallbackTemplate`)

Used when the schema cannot be loaded or the original operation is not found (e.g., the operation was deleted from the schema). Builds a minimal `JsonObject` from stored values:

- `query` section if the stored URL has query params
- `headers` section if the stored entry had non-Content-Type headers
- `body` section if the stored entry had a request body

---

## Without `--edit`

Stored values are used directly. `Content-Type` is excluded from `requestHeaders` when copying from the stored entry — it will be re-added automatically when `requestBody` is attached as `StringContent`.

---

## URL resolution in edit mode

```csharp
var effectivePath = pathPattern ?? new Uri(historyEntry.Url).AbsolutePath;
url = RequestHelpers.BuildUrl(serviceEntry.BaseUrl, effectivePath, pathParams, queryParams);
```

`pathPattern` comes from the schema lookup. If the schema is unavailable, the stored URL's absolute path is used as a fallback.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found` + fuzzy suggestion | 1 |
| History entry not found | `✕ Entry #N not found in <service> history` | 1 |
| Editor cancelled | `⚠ Cancelled` | 1 |
| Invalid JSON in editor | `✕ Invalid JSON in editor: ...` | 1 |
| Network failure | `✕ Could not connect to ...` | 1 |

Schema load failures in edit mode are silently caught — the fallback template is used instead.
