# RequestHelpers

**File:** `src/apix/Helpers/RequestHelpers.cs`  
**Type:** `public static class`

---

## Responsibility

Parses the JSON editor input into typed request components, and constructs the final HTTP URL from a base URL, path pattern, and parameter dictionaries.

Used by `CallCommand` and `ReplayCommand`.

---

## Methods

### `TryParseInputs(string json, out ..., out string? error)` → `bool`

Parses the JSON string written by the user in the editor into four output parameters:

| Output | Type | Source key in JSON |
|---|---|---|
| `pathParams` | `Dictionary<string, string>` | `"path"` object |
| `queryParams` | `Dictionary<string, string>` | `"query"` object |
| `headers` | `Dictionary<string, string>` | `"headers"` object |
| `body` | `string?` | `"body"` node (re-serialized to compact JSON) |

All dictionaries use `OrdinalIgnoreCase` key comparison. Missing sections result in empty dictionaries / null body. Non-object sections are silently ignored (values coerced via `ToString()`).

Returns `false` and sets `error` on JSON parse failure or if root is not an object.

**Note:** `OpenReplayCommand` does NOT use this method when in `--edit` mode because the open replay template includes extra `"url"` and `"method"` keys. It uses a custom `TryParseEditTemplate` instead.

### `BuildUrl(string baseUrl, string pathPattern, Dictionary<string, string> pathParams, Dictionary<string, string> queryParams)` → `string`

1. Substitutes `{param}` placeholders in `pathPattern` using `Uri.EscapeDataString(value)` (case-insensitive)
2. Concatenates `baseUrl.TrimEnd('/') + path`
3. Appends query string via `HttpUtility.ParseQueryString` if `queryParams` is non-empty

```
baseUrl  = "https://api.example.com"
path     = "/pets/{petId}"
pathParams = { "petId": "42" }
queryParams = { "format": "json" }
→ "https://api.example.com/pets/42?format=json"
```
