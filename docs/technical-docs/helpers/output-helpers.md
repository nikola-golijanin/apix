# OutputHelpers

**File:** `src/apix/Helpers/OutputHelpers.cs`  
**Type:** `internal static class`

---

## Responsibility

Shared display utilities used by `HistoryCommand`, `CallCommand`, `OpenCommand`, `OpenHistoryCommand`, and `OpenReplayCommand` for consistent terminal formatting.

---

## Methods

### `Separator(int width)`

Prints a grey horizontal rule of `─` characters at the given width.

```
────────────────────────────────────────
```

### `PrintHeaders(Dictionary<string, string> headers)`

Prints a `Headers:` label followed by key-value pairs. Column width is padded to `maxKeyLength + 4`. Does nothing if the dictionary is empty.

```
  Headers:
    Content-Type    application/json
    Authorization   Bearer ...
```

### `PrintBody(string body, bool full)` → `bool`

1. Attempts to parse `body` as JSON and re-serialize with `WriteIndented = true` for pretty-printing. Falls back to raw string on parse failure.
2. Splits into lines. If `full = false` and the body exceeds **40 lines**, truncates to the first 40.
3. Prints each line with a 2-space indent.
4. Returns `true` if the body was truncated, `false` otherwise.

Callers use the return value to print a truncation notice:

```
  (truncated — run with --verbose to see full body)
```

### `FormatAge(DateTimeOffset timestamp)` → `string`

Converts a timestamp to a human-readable relative age:

| Elapsed | Output |
|---|---|
| < 60 seconds | `"just now"` |
| < 1 hour | `"N min ago"` |
| < 24 hours | `"N hr ago"` |
| ≥ 24 hours | `"N days ago"` |
