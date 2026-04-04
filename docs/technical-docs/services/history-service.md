# HistoryService

**File:** `src/apix/Services/HistoryService.cs`  
**Type:** `public static class` (no DI registration needed)

---

## Responsibility

Owns all reads and writes for per-service (and open-request) history files at `~/.apix/history/<service>.json`.

---

## Storage paths

```csharp
RootDir      = ~/.apix/
HistoryPath  = ~/.apix/history/{service}.json
```

The `_open` sentinel key (`OpenCommand.HistoryKey = "_open"`) is used for schema-free open requests, stored at `~/.apix/history/_open.json`.

---

## Methods

### `AppendAsync(string service, HistoryEntry entry)` → `int`

1. `LoadAllAsync(service)` — loads existing entries (empty list if file absent)
2. Computes `nextId = max(existing ids) + 1` (starts at 1 for empty history)
3. Creates a new record via `entry with { Id = nextId }`
4. `SaveAsync(service, entries)` — serializes and writes the file
5. Returns the assigned `nextId`

### `LoadAllAsync(string service)` → `List<HistoryEntry>`

Reads and deserializes the history file. Returns empty list if the file does not exist.

### `FindAsync(string service, int id)` → `HistoryEntry?`

`LoadAllAsync(service)` then `List.Find(e => e.Id == id)`.

### `SaveAsync` (private)

1. `Directory.CreateDirectory(history/)` — creates directory if absent
2. Serializes `new HistoryRoot(entries)` and writes to `HistoryPath(service)`

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

## Internal wrapper

```csharp
private record HistoryRoot(List<HistoryEntry> Entries);
```

The JSON file wraps the entries array: `{ "entries": [...] }`.

---

## `HistoryEntry` model

`HistoryEntry.OperationId` is `string?` (nullable) to support open requests that have no associated OpenAPI operation. All other commands set it to the operation ID string.

```csharp
public record HistoryEntry(
    int Id, DateTimeOffset Timestamp, string Method, string Url,
    string? OperationId,
    Dictionary<string, string> RequestHeaders, string? RequestBody,
    int StatusCode, string StatusText,
    Dictionary<string, string> ResponseHeaders, string? ResponseBody,
    long DurationMs
);
```
