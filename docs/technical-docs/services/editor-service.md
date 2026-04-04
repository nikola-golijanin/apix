# EditorService

**File:** `src/apix/Services/EditorService.cs`  
**Type:** `public static class` (no DI registration needed)

---

## Responsibility

- Builds JSON editor templates from OpenAPI operation schemas
- Resolves the editor executable (config → `$EDITOR` → OS default)
- Launches the editor as a blocking subprocess and returns the edited content

---

## Public API

### `KnownPresets`

```csharp
public static readonly string[] KnownPresets = ["vim", "nano", "vscode", "notepad"];
```

Used by `ConfigGetCommand.PrintEditor` and `ConfigListCommand` for the preset hint line.

### `ExpandPreset(string value)` → `string`

Expands shorthand preset names to full executable strings:

| Input | Output |
|---|---|
| `"vscode"` | `"code --wait"` |
| anything else | unchanged |

### `ResolveEditorAsync()` → `string`

Resolution order:
1. `ConfigService.GetEditorAsync()` — user-configured value; expanded via `ExpandPreset` if set
2. `$EDITOR` environment variable
3. OS default: `"notepad"` on Windows, `"nano"` on Linux/macOS

### `GenerateTemplate(OpenApiOperation op)` → `JsonObject?`

Builds a template object for the editor. Returns `null` if the operation has no path params, query params, header params, or JSON request body (editor is skipped entirely in that case).

**Template structure:**

```json
{
  "path":    { "<param>": "<hint>" },   // only if path params exist
  "query":   { "<param>": "<hint>" },   // only if query params exist
  "headers": { "<param>": "<hint>" },   // only if header params exist
  "body":    { ... }                    // only if application/json body exists
}
```

Sections are omitted when empty. Body is built recursively via `SchemaToNode`.

### `OpenForEditAsync(JsonObject template, CancellationToken)` → `Task<string>`

1. Creates a temp file, renames it to `<tmp>.json` (for editor syntax highlighting)
2. Serializes `template` with `WriteIndented = true` and `UnsafeRelaxedJsonEscaping`
3. Calls `ResolveEditorAsync()` then `LaunchEditorAsync`
4. Reads and returns the file contents after the editor exits
5. Deletes the temp file in `finally`

---

## Internal methods

### `LaunchEditorAsync(string editor, string filePath, CancellationToken)`

- Splits `editor` on first space into `executable` + `argsPrefix` (handles `"code --wait"`)
- **VS Code guard:** if executable contains `"code"` but `argsPrefix` doesn't contain `"--wait"`, appends `--wait` automatically
- **Windows:** wraps in `cmd.exe /c <executable> <args> "<file>"` so `.cmd` batch files (like `code.cmd`) resolve correctly
- **Linux/macOS:** launches executable directly
- Awaits `process.WaitForExitAsync(cancellationToken)`

### `SchemaToNode(IOpenApiSchema? raw, int depth)` → `JsonNode`

Recursively converts a schema to a JSON template node:

| Schema type | Output |
|---|---|
| Enum | `JsonValue` with first enum value as string |
| Object (has properties or allOf/anyOf/oneOf) | Nested `JsonObject`; depth guard at 6 returns `"object"` |
| Array / has `items` | `JsonArray` with one element from `SchemaToNode(items)` |
| Scalar | `JsonValue` from `SchemaToHint` |

### `SchemaToHint(IOpenApiSchema? raw, bool required)` → `string`

Returns a type hint string for scalar/simple schemas. Appends `?` suffix when `required = false`.

| Schema | required=true | required=false |
|---|---|---|
| integer | `"integer"` | `"integer?"` |
| number | `"number"` | `"number?"` |
| boolean | `"boolean"` | `"boolean?"` |
| enum | `"a \| b \| c"` | `"a \| b \| c?"` |
| object | `"object"` | `"object?"` |
| string (default) | `"string"` | `"string?"` |

### `CollectProperties(IOpenApiSchema)` → `Dictionary<string, IOpenApiSchema>`

Merges properties from:
1. `schema.Properties` (direct)
2. All sub-schemas from `allOf`, `anyOf`, `oneOf` (resolved via `Resolve`)

Case-insensitive dictionary; later entries overwrite earlier ones.

### `Resolve(IOpenApiSchema?)` → `IOpenApiSchema?`

Unwraps `OpenApiSchemaReference` to its `Target`. Returns schema as-is for non-reference types.

---

## JSON options

```csharp
private static readonly JsonSerializerOptions PrettyJson = new()
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
```

`UnsafeRelaxedJsonEscaping` prevents unnecessary Unicode escaping of characters like `<`, `>`, `&` in template values.
