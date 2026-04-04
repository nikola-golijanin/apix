# ImportCommand

**File:** `src/apix/Commands/ImportCommand.cs`  
**Registered as:** `apix import`  
**Base class:** `AsyncCommand<ImportCommand.Settings>`  
**DI:** `IHttpClientFactory` (constructor injection)

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Name` | `string` | `-n\|--name` | Required. Local registry name |
| `BaseUrl` | `string?` | `--base-url` | Required (validated in `Validate()`). Base URL for all requests |
| `Schema` | `string` | `-s\|--schema` | Required. URL or local file path to the OpenAPI schema |

`Validate()` is overridden to enforce `--base-url` even though it is declared as `string?` — this avoids requiring the Spectre attribute workaround for options that must be present.

---

## Execution flow

```
LoadSchemaBytesAsync(schema)
  ├─ URL? → httpClient.GetByteArrayAsync
  └─ File? → File.ReadAllBytesAsync

OpenApiJsonReader.ReadAsync(schemaBytes)
  → parse document, count endpoints, detect spec version

ServiceRegistry.UpsertAsync(entry, schemaBytes)
  → write schema file, update services.json
```

Everything inside `ExecuteAsync` runs under `AnsiConsole.Status().StartAsync` so the user sees a spinner during network I/O and schema parsing.

---

## Schema loading (`LoadSchemaBytesAsync`)

Determines source type by attempting `Uri.TryCreate` with `http`/`https` scheme check:

- **URL**: Downloads via `IHttpClientFactory`. On failure returns a formatted error string.
- **File**: Checks `File.Exists`. Reads with `File.ReadAllBytesAsync`.

Returns `(byte[]? bytes, bool isUrl, string? error)` — callers check `error` before proceeding.

---

## Schema source recording

After a successful parse the source is recorded as either:

```csharp
new SchemaSource(SchemaSourceType.Url, settings.Schema)
// or
new SchemaSource(SchemaSourceType.File, Path.GetFullPath(settings.Schema))
```

`Path.GetFullPath` is used for file paths so that `service update` can re-locate the file later regardless of the working directory at time of call.

---

## Error handling

| Condition | Output |
|---|---|
| Schema URL unreachable | `✕ Could not reach schema URL: ...` |
| File not found | `✕ File not found: ...` |
| Parse errors or null document | `✕ Invalid OpenAPI schema — failed to parse document` |

All errors return exit code `1`.
