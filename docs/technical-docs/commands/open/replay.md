# OpenReplayCommand

**File:** `src/apix/Commands/Open/OpenReplayCommand.cs`  
**Registered as:** `apix open replay`  
**Base class:** `AsyncCommand<OpenReplayCommand.Settings>`  
**DI:** `IHttpClientFactory` (constructor injection)

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Id` | `int` | arg 0 `<id>` | Required. History entry to replay |
| `Edit` | `bool` | `-e\|--edit` | Open editor pre-filled with stored values |
| `NoSave` | `bool` | `--no-save` | Skip saving to history |
| `Verbose` | `bool` | `-v\|--verbose` | Print full headers and body |

---

## Execution flow

```
1. HistoryService.FindAsync(OpenCommand.HistoryKey, id)

2a. if --edit:
    BuildEditTemplate(historyEntry)
      → { "url": "...", "method": "...", "headers": {...}, "body": {...} }
    EditorService.OpenForEditAsync(template)
    TryParseEditTemplate(editedJson)
      → parsedUrl, parsedMethod, parsedHeaders, requestBody

2b. if no --edit:
    url = historyEntry.Url
    method = historyEntry.Method
    requestHeaders ← historyEntry.RequestHeaders (excluding Content-Type)
    requestBody = historyEntry.RequestBody

3. Build HttpRequestMessage → execute → print → HistoryService.AppendAsync
```

---

## Key difference from `ReplayCommand`

`ReplayCommand` pre-fills an editor template derived from the **OpenAPI schema** (with type hints for each field). `OpenReplayCommand` has no schema, so it pre-fills with raw stored values. The edit template is a plain JSON object with `url` and `method` as editable top-level fields.

---

## Edit template (`BuildEditTemplate`)

```json
{
  "url": "https://httpbin.org/post",
  "method": "POST",
  "headers": {
    "Authorization": "Bearer ..."
  },
  "body": { ... }
}
```

- `Content-Type` is excluded from the headers section — it will be automatically set when `requestBody` is attached.
- `body` is deep-cloned from the stored `RequestBody` via `JsonNode.Parse().DeepClone()`.

---

## Edit template parsing (`TryParseEditTemplate`)

Custom parser (not `RequestHelpers.TryParseInputs`) because the template has `url` and `method` as top-level fields that standard `TryParseInputs` does not handle.

```csharp
url    = rootObj["url"]?.GetValue<string>();
method = rootObj["method"]?.GetValue<string>();
// headers and body parsed the same way as TryParseInputs
```

If the user clears `url` or `method`, the stored values are used as fallback:

```csharp
url    = parsedUrl    ?? historyEntry.Url;
method = new HttpMethod((parsedMethod ?? historyEntry.Method).ToUpperInvariant());
```

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Entry not found | `✕ No open request found with id #N` | 1 |
| Editor cancelled | `⚠ Cancelled` | 1 |
| Invalid JSON in editor | `✕ Invalid JSON in editor: ...` | 1 |
| Network failure | `✕ Could not connect to ...` | 1 |
