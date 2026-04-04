# EndpointListCommand

**File:** `src/apix/Commands/Endpoint/EndpointListCommand.cs`  
**Registered as:** `apix endpoint list`  
**Base class:** `AsyncCommand<EndpointListCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Service` | `string` | arg 0 `<service>` | Required. Registered service name |
| `Method` | `string?` | `-m\|--method` | Optional. Filter by HTTP method (uppercased for comparison) |
| `Tag` | `string?` | `-t\|--tag` | Optional. Filter by OpenAPI tag (case-insensitive) |
| `Quiet` | `bool` | `-q\|--quiet` | Tab-separated raw output for piping |

---

## Execution flow

1. `ServiceRegistry.FindAsync(service)` → resolve service entry
2. `ServiceRegistry.OpenSchema(service)` → stream; `OpenApiJsonReader.ReadAsync` → document
3. Flatten all operations from `doc.Paths` into `(Method, Path, OperationId, Tags)` tuples
4. Apply `--method` and `--tag` filters (both case-insensitive)
5. Render table or quiet output

---

## Column width calculation

All column widths are content-driven — computed as `max(header.Length, entries.Max(e => field.Length))`:

- `verbWidth` is fixed at 7 (length of "DELETE", the longest standard verb)
- `routeWidth`, `opIdWidth`, `tagWidth` are computed from data

No terminal-width capping is applied (unlike `HistoryCommand`) because endpoint paths are expected to be short.

---

## Method color coding

| Method | Color |
|---|---|
| GET | green |
| POST | blue |
| PUT | yellow |
| DELETE | red |
| PATCH | magenta |
| other | white |

---

## Quiet mode

Writes `method\tpath\toperationId` per operation to `Console.WriteLine`.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found` + fuzzy suggestion | 1 |
| Schema parse failure | `✕ Failed to read stored schema` | 1 |
| No matching operations (with filters) | Empty state message (not an error) | 0 |
