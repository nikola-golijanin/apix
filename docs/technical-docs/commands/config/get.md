# ConfigGetCommand

**File:** `src/apix/Commands/Config/ConfigGetCommand.cs`  
**Registered as:** `apix config get`  
**Base class:** `AsyncCommand<ConfigGetCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Key` | `string` | arg 0 `<key>` | Config key to retrieve |

---

## `PrintEditor` (internal static)

Shared by `ConfigGetCommand` and `ConfigListCommand`. Handles two states:

- **Not set**: prints `editor  (not set — using $EDITOR or nano/notepad)`; fallback label is OS-dependent
- **Set**: prints `editor  <value>`, appends `→ <expanded>` if the value is a known preset that expands to something different, appends `(custom)` for non-preset values
