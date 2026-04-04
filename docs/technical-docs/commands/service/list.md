# ServiceListCommand

**File:** `src/apix/Commands/Service/ServiceListCommand.cs`  
**Registered as:** `apix service list`  
**Base class:** `AsyncCommand<ServiceListCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Quiet` | `bool` | `-q\|--quiet` | Tab-separated raw output for piping |

---

## Execution flow

1. `ServiceRegistry.LoadAllAsync()` → `List<ServiceEntry>`
2. If empty:
   - Quiet mode: return 0 silently
   - Normal mode: print empty state message
3. If quiet: write `name\tbaseUrl\tendpointCount` per entry to `Console.WriteLine`
4. Normal mode: render a Spectre `Table` with columns Name, Base URL, Endpoints

---

## Notes

Uses `AnsiConsole.Write(table)` — Spectre renders the box-drawing table automatically. No manual width calculation needed since the table handles column sizing.
