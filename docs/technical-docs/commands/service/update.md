# ServiceUpdateCommand

**File:** `src/apix/Commands/Service/ServiceUpdateCommand.cs`  
**Registered as:** `apix service update`  
**Base class:** `AsyncCommand<ServiceUpdateCommand.Settings>`  
**DI:** `IHttpClientFactory` (constructor injection)

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Name` | `string` | arg 0 `<name>` | Required. Service name to update |
| `File` | `string?` | `-f\|--file` | Optional. Path to a new local schema file |

---

## Source mode resolution

| Condition | Mode |
|---|---|
| `--file` provided | File mode — read from the given path |
| No `--file`, service was imported from URL | URL mode — re-fetch from `entry.SchemaSource.Value` |
| No `--file`, service was imported from file | Error — cannot auto-update |

---

## Execution flow

```
1. ServiceRegistry.FindAsync(name)
2. Validate source mode (error if file-imported and no --file)
3. LoadNewBytesAsync → byte[] newBytes
4. OpenApiJsonReader.ReadAsync(newBytes) → newDoc

5. ServiceRegistry.OpenSchema(name) → old schema stream
   OpenApiJsonReader.ReadAsync(oldStream) → oldDoc

6. ExtractOperations(oldDoc) + ExtractOperations(newDoc)
   → compute added / removed lists

7. ServiceRegistry.UpsertAsync(newEntry, newBytes)
```

All I/O in steps 3–4 runs inside `AnsiConsole.Status().StartAsync`.

---

## Diff computation (`ExtractOperations`)

Extracts a flat list of `(Method, Path, OperationId)` tuples from an `OpenApiDocument`. Added/removed sets are computed by LINQ set difference on `(Method, Path)` pairs — operation ID is carried for display but not used in comparison.

```csharp
var added   = newOps.Where(n => !oldOps.Any(o => o.Method == n.Method && o.Path == n.Path));
var removed = oldOps.Where(o => !newOps.Any(n => n.Method == o.Method && n.Path == o.Path));
```

If both sets are empty, prints "No changes detected" and exits without writing.

---

## Schema source update

When in file mode, the stored `SchemaSource` is replaced with the new absolute path:

```csharp
new SchemaSource(SchemaSourceType.File, Path.GetFullPath(settings.File!))
```

When in URL mode, the existing `SchemaSource` is preserved unchanged.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found` + fuzzy suggestion | 1 |
| File-imported, no `--file` | `✕ Cannot update — no URL available` | 1 |
| File not found | `✕ File not found: ...` | 1 |
| URL unreachable | `✕ Could not reach schema URL: ...` | 1 |
| Parse failure | `✕ Invalid OpenAPI schema` | 1 |
