# ConfigListCommand

**File:** `src/apix/Commands/Config/ConfigListCommand.cs`  
**Registered as:** `apix config list`  
**Base class:** `AsyncCommand<ConfigListCommand.Settings>`  
**DI:** none

---

## Settings

No settings — `Settings` is an empty `CommandSettings` subclass.

---

## Execution

1. `ConfigService.GetEditorAsync()` — loads current editor value
2. Prints header + separator via `OutputHelpers.Separator(40)`
3. Delegates to `ConfigGetCommand.PrintEditor(stored)` for the editor row
4. Prints the preset hint line: `Presets: vim · nano · vscode · notepad`

`EditorService.KnownPresets` supplies the preset list so there is no duplication between the display and the actual resolution logic.
