# EndpointDetailCommand

**File:** `src/apix/Commands/Endpoint/EndpointDetailCommand.cs`  
**Registered as:** `apix endpoint details`  
**Base class:** `AsyncCommand<EndpointDetailCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Service` | `string` | arg 0 `<service>` | Required. Registered service name |
| `OperationId` | `string` | arg 1 `<operationId>` | Required. Operation to inspect |

---

## Execution flow

1. Resolve service → open schema → parse document → `FindOperation`
2. Separate parameters by location: Path, Query, Header
3. Build display URL: `pathPattern` + inline query string (`?key=...&key2=...`)
4. Print sections:
   - Header line: `service — operationId`
   - `METHOD  path?query`
   - Path Parameters (if any)
   - Query Parameters (if any)
   - Headers (if any)
   - Request Body template (if any)
   - Responses (if any)

---

## Parameter display (`PrintParamSection`)

Renders a table of parameters with three columns: **Name**, **Type hint**, **Required** (✔ checkmark for required fields). Column widths are content-driven.

Type hints come from `EditorService.SchemaToHint(p.Schema, p.Required)` — reuses the same hint generation logic as the editor template (e.g., `"string"`, `"integer?"`, `"Buy | Sell"`).

---

## Request body display

Uses `EditorService.GenerateTemplate(op)` and extracts the `"body"` key. The resulting `JsonNode` is serialized with `WriteIndented = true` and `UnsafeRelaxedJsonEscaping` — the same options used when writing editor templates.

---

## Responses

Iterates `op.Responses` ordered by status code key. Prints `code  description` for each.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found` | 1 |
| Schema parse failure | `✕ Failed to read stored schema` | 1 |
| Operation not found | `✕ Operation not found: <operationId>` | 1 |
