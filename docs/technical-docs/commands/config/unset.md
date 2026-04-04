# ConfigUnsetCommand

**File:** `src/apix/Commands/Config/ConfigUnsetCommand.cs`  
**Registered as:** `apix config unset`  
**Base class:** `AsyncCommand<ConfigUnsetCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Key` | `string` | arg 0 `<key>` | Config key to unset |

---

## Execution

Calls `ConfigService.UnsetEditorAsync()` which sets `Editor = null` in `AppConfig` and re-serializes. The config file is preserved — it is not deleted even when all values are unset.
