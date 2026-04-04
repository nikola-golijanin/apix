# ServiceRegistry

**File:** `src/apix/Services/ServiceRegistry.cs`  
**Type:** `public class` (static methods only, no DI registration needed)

---

## Responsibility

Owns all reads and writes for:
- The service registry index (`~/.apix/services.json`)
- The per-service raw OpenAPI schema files (`~/.apix/schemas/<name>.json`)

---

## Storage paths

```csharp
RootDir      = ~/.apix/
RegistryPath = ~/.apix/services.json
SchemaPath   = ~/.apix/schemas/{name}.json
```

---

## Methods

### `UpsertAsync(ServiceEntry entry, byte[] schemaBytes)`

1. `Directory.CreateDirectory(schemas/)` — creates the directory if absent
2. `File.WriteAllBytesAsync(SchemaPath(name), schemaBytes)` — always overwrites the schema file
3. `LoadAllAsync()` → find existing entry by name (case-insensitive) → replace or append
4. `SaveRegistryAsync(entries)` — serializes and writes `services.json`

Used by `ImportCommand` and `ServiceUpdateCommand`.

### `LoadAllAsync()` → `List<ServiceEntry>`

Reads and deserializes `services.json`. Returns empty list if the file does not exist.

### `FindAsync(string name)` → `ServiceEntry?`

`LoadAllAsync()` then `List.Find` with `OrdinalIgnoreCase` name comparison.

### `OpenSchema(string name)` → `Stream`

Returns a `FileStream` opened for reading on the schema file. Callers are responsible for disposing (`await using`).

### `RemoveAsync(string name)`

1. Removes all entries matching `name` (case-insensitive) from the list
2. `SaveRegistryAsync`
3. Deletes the schema file if it exists

Does **not** delete the history file for the service.

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

`JsonStringEnumConverter` serializes `SchemaSourceType` as `"Url"` / `"File"` strings rather than integers.

---

## Internal wrapper

```csharp
private record RegistryRoot(List<ServiceEntry> Services);
```

The JSON file wraps the services array: `{ "services": [...] }`.
