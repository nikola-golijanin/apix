# StringHelpers

**File:** `src/apix/Helpers/StringHelpers.cs`  
**Type:** `internal static class`

---

## Responsibility

Fuzzy string matching for "did you mean?" suggestions shown when an unknown service name or operation ID is provided.

---

## Methods

### `FindClosestMatch(string input, IEnumerable<string> candidates, int maxDistance = 2)` → `string?`

Iterates over all candidates and computes the Levenshtein edit distance from `input` to each. Returns the candidate with the lowest distance if that distance is ≤ `maxDistance`. Returns `null` if no candidate is within the threshold.

**Default threshold:** 2 edits (catches single typos and transpositions).

Used by:
- `CallCommand` — suggests closest operation ID when the given ID is not found
- `ServiceRemoveCommand` / `ServiceUpdateCommand` — suggests closest service name

### `Levenshtein(string a, string b)` → `int` (private)

Standard dynamic-programming implementation. Uses a 2D matrix of size `(a.Length+1) × (b.Length+1)`. Substitution cost is 0 for matching characters, 1 for mismatches. Insertion and deletion cost 1.

Edge cases: returns `b.Length` if `a` is empty, `a.Length` if `b` is empty.
