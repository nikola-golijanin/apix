# ConfigService

**File:** `src/apix/Services/ConfigService.cs`  
**Type:** `public static class` (no DI registration needed)

---

## Responsibility

Owns all reads and writes for the global config file at `~/.apix/config.json`.

---

## Storage paths

```csharp
RootDir    = ~/.apix/
ConfigPath = ~/.apix/config.json
```

---

## Methods

### `GetEditorAsync()` → `Task<string?>`

`LoadAsync()` → `config?.Editor`. Returns `null` if config file does not exist or `Editor` is unset.

### `SetEditorAsync(string editor)`

1. `LoadAsync()` → existing config (or `new AppConfig(null)` if absent)
2. `config with { Editor = editor }` — creates updated record
3. `Directory.CreateDirectory(RootDir)` — ensures `~/.apix/` exists
4. Serializes and writes to `config.json`

### `UnsetEditorAsync()`

Same flow as `SetEditorAsync` but sets `Editor = null`. The config file is preserved (not deleted) — it remains as `{ "editor": null }`.

### `LoadAsync` (private) → `Task<AppConfig?>`

Returns `null` if `config.json` does not exist. Otherwise deserializes and returns the `AppConfig` record.

---

## Internal record

```csharp
private record AppConfig(string? Editor);
```

The JSON file serializes to:

```json
{
  "editor": "vim"
}
```

---

## JSON serialization

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};
```

---

## Extension points

Currently only `Editor` is stored. Adding new config keys requires:
1. Adding a property to `AppConfig`
2. Adding `GetXAsync` / `SetXAsync` / `UnsetXAsync` methods
