# ServiceRemoveCommand

**File:** `src/apix/Commands/Service/ServiceRemoveCommand.cs`  
**Registered as:** `apix service remove`  
**Base class:** `AsyncCommand<ServiceRemoveCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Name` | `string` | arg 0 `<name>` | Required. Service name to remove |

---

## Execution flow

1. `ServiceRegistry.FindAsync(name)` — verifies the service exists before prompting
2. `AnsiConsole.Confirm(...)` — interactive yes/no prompt with `defaultValue: false`
3. If confirmed: `ServiceRegistry.RemoveAsync(name)` — deletes from `services.json` and removes `schemas/<name>.json`
4. If declined: return 0 silently

---

## Notes

The confirmation defaults to **no** (`defaultValue: false`) to prevent accidental data loss. `RemoveAsync` does not delete history files — `~/.apix/history/<name>.json` is left on disk. This is intentional: history files are cheap to keep and may be referenced later.

---

## Error handling

| Condition | Output | Exit |
|---|---|---|
| Service not found | `✕ Service not found: <name>` | 1 |
