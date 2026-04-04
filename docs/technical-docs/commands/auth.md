# Auth commands (stubs)

**Files:** `src/apix/Commands/Auth/AuthSetCommand.cs`, `AuthRemoveCommand.cs`  
**Registered as:** `apix auth set`, `apix auth remove`  
**Base class:** `Command<Settings>` (synchronous, not async)  
**DI:** none

---

## Current state

Both commands are **stubs** — they print `"auth set: hello world"` / `"auth remove: hello world"` and return 0. No settings are declared; `Settings` is an empty `CommandSettings` subclass.

---

## Intended behaviour (not yet implemented)

Per the proposal:

- `auth set <service> --bearer <token>` — stores a Bearer token in `~/.apix/auth.json`, keyed by service name. The token is injected as `Authorization: Bearer <token>` into all `call` requests for that service.
- `auth remove <service>` — removes the stored token for a service.

When implemented, `CallCommand` and `OpenCommand` will need to check `auth.json` and attach the token before sending.
