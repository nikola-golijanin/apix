# ConfigSetCommand

**File:** `src/apix/Commands/Config/ConfigSetCommand.cs`  
**Registered as:** `apix config set`  
**Base class:** `AsyncCommand<ConfigSetCommand.Settings>`  
**DI:** none

---

## Settings

| Member | Type | Source | Description |
|---|---|---|---|
| `Key` | `string` | arg 0 `<key>` | Config key to set |
| `Value` | `string` | arg 1 `<value>` | Value to assign |

---

## Supported keys

Currently only `editor` is implemented. Unknown keys print an error listing supported keys and return exit code 1.

**`editor` handling:**

1. `ConfigService.SetEditorAsync(value)` — persists to `~/.apix/config.json`
2. `EditorService.ExpandPreset(value)` — expands known presets (e.g., `vscode` → `code --wait`)
3. If expanded differs from stored value, prints `→ launches as: <expanded>`
