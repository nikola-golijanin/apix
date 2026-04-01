# CLI API Explorer — Product Proposal (v1)

## Overview

CLI API Explorer is a developer-focused command-line tool for discovering, calling, and tracking HTTP API endpoints across multiple services. It imports OpenAPI schemas, maintains a local service registry, and provides a clean terminal interface for interacting with APIs — without opening a browser or Postman.

Designed for teams working with many services, it removes friction from day-to-day API testing and debugging.

---

## Core Concepts

- **Service** — a named entry in the local registry, consisting of a name, base URL, and imported OpenAPI schema
- **Endpoint** — an individual HTTP operation parsed from the schema (method + path + operationId)
- **Request** — an executed HTTP call, stored in local history per service
- **Body** — for POST/PUT/PATCH requests, constructed interactively via `$EDITOR`

---

## Commands

### ✅ `import` — Register a service

Imports an OpenAPI schema and registers the service locally.

```
apix import --name <name> --base-url <url> --schema <path|url>
```

**Examples**

```
apix import --name orderService --base-url https://api.orders.internal --schema ./openapi.json
apix import --name orderService --base-url https://api.orders.internal --schema https://api.orders.internal/swagger/v1/swagger.json
```

**Output**

```
Importing orderService...

  ✓ Schema loaded     (OpenAPI 3.0.1)
  ✓ 24 endpoints parsed
  ✓ Saved to registry

orderService is ready. Run [apix endpoints list orderService] to explore.
```

**Errors**

```
✕ Could not reach schema URL: https://api.orders.internal/swagger/v1/swagger.json
  → Check the URL or use a local file path with --schema ./openapi.json
```

```
✕ Invalid OpenAPI schema — failed to parse document
  → Ensure the file is a valid OpenAPI 3.0 or 2.0 specification
```

---

### ✅ `service list` — List registered services

```
apix service list
```

**Output**

```
┌──────────────────┬────────────────────────────────────┬───────────┐
│ Name             │ Base URL                           │ Endpoints │
├──────────────────┼────────────────────────────────────┼───────────┤
│ orderService     │ https://api.orders.internal        │ 24        │
│ paymentService   │ https://api.payments.internal      │ 11        │
│ identityService  │ https://api.identity.internal      │ 8         │
└──────────────────┴────────────────────────────────────┴───────────┘
```

**Empty state**

```
No services registered yet.
Run [apix import] to add your first service.
```

---

### ✅ `service remove` — Unregister a service

```
apix service remove <name>
```

**Output**

```
? Remove orderService and all its history? [y/n]: y

✓ orderService removed.
```

---

### ✅ `service update` — Refresh a service schema

Updates the schema for a registered service. Supports two modes:

- **Auto (URL-based)** — re-fetches from the originally registered schema URL, no extra flags needed
- **Manual (file-based)** — provide a new local schema file via `--file`

```
apix service update <n>
apix service update <n> --file <path>
```

**Examples**

```
apix service update orderService
apix service update orderService --file ./openapi-v2.json
```

**Output — changes detected (URL-based)**

```
Updating orderService...

  ✓ Schema re-fetched     (OpenAPI 3.0.1)
  ✓ 2 endpoints added
    +  POST  /orders/{orderId}/cancel     cancel-order
    +  GET   /orders/{orderId}/timeline   get-order-timeline
  ✓ 1 endpoint removed
    -  GET   /orders/legacy               list-orders-legacy
  ✓ Registry updated

orderService is up to date.
```

**Output — changes detected (file-based)**

```
Updating orderService from file...

  ✓ Schema loaded         (OpenAPI 3.0.1)
  ✓ 1 endpoint added
    +  GET   /orders/{orderId}/timeline   get-order-timeline
  ✓ Registry updated

orderService is up to date.
```

**Output — no changes**

```
Updating orderService...

  ✓ Schema re-fetched     (OpenAPI 3.0.1)
  ✓ No changes detected — registry is already up to date.
```

**Errors**

```
✕ Cannot update orderService — schema was imported from a local file and no URL is available.
  → Provide a new schema file with --file:
    apix service update orderService --file ./openapi.json
```

```
✕ Could not reach schema URL: https://api.orders.internal/swagger/v1/swagger.json
  → Service may be unavailable. Try again or provide a local file with --file.
```

```
✕ File not found: ./openapi-v2.json
```

---

### ✅ `endpoints list` — List endpoints for a service

```
apix endpoints list <name>
apix endpoints list <name> --method GET
apix endpoints list <name> --tag Orders
```

**Output**

```
orderService — https://api.orders.internal
──────────────────────────────────────────────────────────────────────────────
  Verb     Route                    Operation ID   Tag
──────────────────────────────────────────────────────────────────────────────
  GET      /orders/{orderId}        get-order      orders
  POST     /orders                  create-order   orders
  DELETE   /orders/{orderId}        cancel-order   orders
  GET      /orders                  list-orders    orders
  POST     /payments/initiate       initiate-payment  payments
  GET      /payments/{paymentId}    get-payment    payments
```

Column widths adjust dynamically to fit the widest value in each column.

HTTP methods are color-coded:
- `GET` — green
- `POST` — blue
- `PUT` — yellow
- `DELETE` — red
- `PATCH` — magenta

**With `--method POST` filter**

```
orderService — https://api.orders.internal  [POST]
──────────────────────────────────────────────────────────────────────────────
  Verb     Route                    Operation ID      Tag
──────────────────────────────────────────────────────────────────────────────
  POST     /orders                  create-order      orders
  POST     /payments/initiate       initiate-payment  payments
```

**Errors**

```
✕ Service not found: orderService
  → Run [apix service list] to see registered services.
```

---

### `call` — Execute a request

```
apix call <service> <operationId>
apix call <service> <operationId> --param <key=value> --param <key=value>
apix call <service> <operationId> --body '<json>'
apix call <service> <operationId> --body @./payload.json
apix call <service> <operationId> --verbose
apix call <service> <operationId> --full
apix call <service> <operationId> --no-save
```

**Flags**

| Flag | Description |
|---|---|
| `--param key=value` | Pass path or query parameters |
| `--body '<json>'` | Provide request body inline |
| `--body @file.json` | Load request body from a file |
| `--verbose` | Print full request and response headers |
| `--full` | Print full response body without truncation |
| `--no-save` | Execute the request but do not save it to history |

**Examples**

```
apix call orderService get-order --param orderId=abc-123
apix call orderService list-orders --param page=1 --param pageSize=20
apix call orderService create-order --body '{"symbol":"BTC","quantity":1,"side":"Buy"}'
apix call orderService create-order --body @./order.json
apix call orderService list-orders --full
apix call orderService get-order --param orderId=abc-123 --no-save
```

**Default output (successful response)**

```
► POST https://api.orders.internal/orders

◆ 201 Created  [143ms]

  Body:
  {
    "orderId": "abc-123",
    "status": "pending",
    "symbol": "BTC",
    "quantity": 1,
    "createdAt": "2025-03-31T10:00:00Z"
  }

  Saved as #4 — [apix history orderService 4] to inspect · [apix replay orderService 4] to replay
```

**With `--verbose` (full headers printed)**

```
─────────────────────────────────────────────────────────────
  REQUEST
─────────────────────────────────────────────────────────────
  POST https://api.orders.internal/orders

  Headers:
    Content-Type    application/json
    Authorization   Bearer eyJhbGc...
    Accept          application/json

  Body:
  {
    "symbol": "BTC",
    "quantity": 1,
    "side": "Buy"
  }

─────────────────────────────────────────────────────────────
  RESPONSE  201 Created  [143ms]
─────────────────────────────────────────────────────────────
  Headers:
    Content-Type    application/json
    Location        /orders/abc-123

  Body:
  {
    "orderId": "abc-123",
    "status": "pending",
    "symbol": "BTC",
    "quantity": 1,
    "createdAt": "2025-03-31T10:00:00Z"
  }

  Saved as #4 — [apix history orderService 4] to inspect · [apix replay orderService 4] to replay
```

**Large response body (truncated by default)**

```
◆ 200 OK  [212ms]

  Body:
  {
    "page": 1,
    "total": 243,
    "items": [
      { "orderId": "abc-001", "status": "pending" },
      { "orderId": "abc-002", "status": "filled" },
      ...
    ]
  }
  ... 201 lines truncated — run [apix history orderService 4 --full] to see full response
  or rerun with --full to print immediately

  Saved as #4 — [apix history orderService 4] to inspect · [apix replay orderService 4] to replay
```

**Error response**

```
► GET https://api.orders.internal/orders/999

✕ 404 Not Found  [87ms]

  Body:
  {
    "error": "Order not found",
    "code": "ORDER_404"
  }

  Saved as #5 — [apix history orderService 5] to inspect · [apix replay orderService 5] to replay
```

**Server error**

```
► POST https://api.orders.internal/payments/initiate

✕ 500 Internal Server Error  [1203ms]

  Body:
  {
    "error": "Unexpected error occurred"
  }

  Saved as #6 — [apix history orderService 6] to inspect · [apix replay orderService 6] to replay
```

**Network failure**

```
✕ Could not connect to https://api.orders.internal
  → Service may be unavailable or base URL is incorrect.
```

**With `--no-save`**

```
► GET https://api.orders.internal/orders/abc-123

◆ 200 OK  [87ms]

  Body:
  {
    "orderId": "abc-123",
    "status": "pending"
  }

  (not saved to history)
```

**No response body (204)**

```
► DELETE https://api.orders.internal/orders/abc-123

◆ 204 No Content  [61ms]

  Saved as #7 — [apix history orderService 7] to inspect · [apix replay orderService 7] to replay
```

#### Body Builder (interactive)

When a request requires a body and `--body` is not provided, the tool generates a JSON template from the schema and opens it in the configured editor:

```
► create-order requires a request body.

  Generating template from schema...
  Opening in editor (code --wait)...

  [user edits and saves the file]

✓ Body loaded — sending request...
```

The generated template pre-fills field names with type hints as placeholder values:

```json
{
  "symbol": "<string>",
  "quantity": "<number>",
  "side": "<string: Buy | Sell>",
  "clientOrderId": "<string?>"
}
```

Required fields are unmarked. Optional fields are annotated with `?`. Enum values are listed inline.

---

### `auth set` — Store authentication for a service

```
apix auth set <service> --bearer <token>
```

**Output**

```
✓ Bearer token saved for orderService.
  Token will be injected into all requests automatically.
```

The token is stored locally and injected as `Authorization: Bearer <token>` on every request to that service.

**Remove auth**

```
apix auth remove <service>
```

```
✓ Auth removed for orderService.
```

---

### `history` — View and inspect past requests

**List recent requests**

```
apix history <service>
```

**Output**

```
orderService — last 10 requests
──────────────────────────────────────────────────────────────────
  #1   POST    /orders                   201    143ms   2 min ago
  #2   GET     /orders/abc-123           200     87ms   2 min ago
  #3   GET     /orders/999               404     61ms   1 min ago
  #4   POST    /payments/initiate        400    203ms   just now
```

Status codes are color-coded:
- `2xx` — green
- `3xx` — cyan
- `4xx` — yellow
- `5xx` — red

**Empty state**

```
No requests made to orderService yet.
Run [apix call orderService <operationId>] to get started.
```

**Inspect a specific request**

```
apix history <service> <id>
apix history <service> <id> --verbose
apix history <service> <id> --full
apix history <service> <id> --request-only
apix history <service> <id> --response-only
apix history <service> <id> --curl
```

**Flags**

| Flag | Description |
|---|---|
| `--verbose` | Include request and response headers |
| `--full` | Print full response body without truncation |
| `--request-only` | Print only the request block |
| `--response-only` | Print only the response block |
| `--curl` | Print the request as a curl command |

**Default inspect output**

```
─────────────────────────────────────────────────────────────
  #4 — 31 Mar 2025  10:42:07
─────────────────────────────────────────────────────────────
  REQUEST
─────────────────────────────────────────────────────────────
  POST https://api.orders.internal/orders

  Body:
  {
    "symbol": "BTC",
    "quantity": 1,
    "side": "Buy"
  }

─────────────────────────────────────────────────────────────
  RESPONSE  201 Created  [143ms]
─────────────────────────────────────────────────────────────
  Body:
  {
    "orderId": "abc-123",
    "status": "pending",
    "symbol": "BTC",
    "quantity": 1,
    "createdAt": "2025-03-31T10:00:00Z"
  }
```

**With `--verbose`**

Same as above but with headers printed under both REQUEST and RESPONSE blocks. Bearer tokens are truncated to `Bearer eyJhbGc...`.

**With `--request-only`**

```
─────────────────────────────────────────────────────────────
  #4 — 31 Mar 2025  10:42:07
─────────────────────────────────────────────────────────────
  REQUEST
─────────────────────────────────────────────────────────────
  POST https://api.orders.internal/orders

  Body:
  {
    "symbol": "BTC",
    "quantity": 1,
    "side": "Buy"
  }
```

**With `--response-only`**

```
─────────────────────────────────────────────────────────────
  #4 — 31 Mar 2025  10:42:07
─────────────────────────────────────────────────────────────
  RESPONSE  201 Created  [143ms]
─────────────────────────────────────────────────────────────
  Body:
  {
    "orderId": "abc-123",
    "status": "pending",
    "symbol": "BTC",
    "quantity": 1,
    "createdAt": "2025-03-31T10:00:00Z"
  }
```

**With `--curl`**

```
curl -X POST https://api.orders.internal/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer eyJhbGc..." \
  -d '{"symbol":"BTC","quantity":1,"side":"Buy"}'
```

---

### `replay` — Re-execute a previous request

```
apix replay <service> <id>
```

**Output**

```
Replaying #3 — GET /orders/999

► GET https://api.orders.internal/orders/999

✕ 404 Not Found  [91ms]

  Body:
  {
    "error": "Order not found",
    "code": "ORDER_404"
  }

  Saved as #8 — [apix history orderService 8] to inspect · [apix replay orderService 8] to replay
```

---
---

### `config` — Manage global configuration

Configure global tool behaviour, such as the preferred editor for body building.

```
apix config set <key> <value>
apix config get <key>
apix config list
```

#### Editor configuration

`apix` opens a body template in an editor when building request bodies interactively. The editor is resolved in this order:

1. `apix config set editor` value (if set)
2. `$EDITOR` environment variable (if set)
3. Falls back to `nano` on Linux/macOS, `notepad` on Windows

**Set editor**

```
apix config set editor vim
apix config set editor nano
apix config set editor "code --wait"
apix config set editor "notepad"
apix config set editor "notepad++"
```

Note: for editors that open a new window (VS Code, Notepad++), the `--wait` flag or equivalent must be included so the CLI knows when editing is complete.

**View current editor**

```
apix config get editor
```

```
editor = code --wait
```

**List all config**

```
apix config list
```

```
editor = code --wait
```

**Reset to default**

```
apix config unset editor
```

```
✓ editor reset to default.
```

---

## Local Storage

All data is stored locally on the developer's machine. No telemetry, no remote sync.

| Data | Location |
|---|---|
| Service registry | `~/.apix/services.json` |
| Request history | `~/.apix/history/<service>.json` |
| Auth tokens | `~/.apix/auth.json` |
| Global config | `~/.apix/config.json` |
| Body templates (temp) | System temp directory, deleted after send |

---

## Out of Scope for v1

The following are explicitly excluded from v1 and may be considered for future releases:

- Multi-environment support (`--env staging`)
- OAuth / client credentials flow
- Response diffing between two requests
- Shell autocomplete for operationIds
- Field-by-field interactive prompt body builder
- Export request as `curl` command
- Collections / saved request groups

---

## Tech Stack

- **.NET 10** — runtime
- **Spectre.Console** — terminal UI, tables, colored output, prompts
- **Microsoft.OpenApi** — OpenAPI schema parsing
- **System.Text.Json** — serialization
- **Local JSON files** — persistence (no database)

---

## Installation & Distribution

`apix` is distributed in two forms targeting different use cases.

### As a dotnet tool (recommended for developers)

Requires .NET 10 runtime installed on the machine.

```bash
dotnet tool install -g apix
```

Update to the latest version:

```bash
dotnet tool update -g apix
```

Uninstall:

```bash
dotnet tool uninstall -g apix
```

Once installed, `apix` is available globally from any terminal session.

### As a self-contained executable

No runtime required. A single binary that runs anywhere.

Download the appropriate binary for your platform from the GitHub Releases page and place it somewhere on your `PATH`.

| Platform | File |
|---|---|
| Windows x64 | `apix-win-x64.exe` |
| Linux x64 | `apix-linux-x64` |
| macOS x64 | `apix-osx-x64` |
| macOS ARM64 | `apix-osx-arm64` |

**Windows example**

```powershell
# Download and move to a directory on PATH
mv apix-win-x64.exe C:\tools\apix.exe
```

**Linux / macOS example**

```bash
chmod +x apix-linux-x64
mv apix-linux-x64 /usr/local/bin/apix
```

### Publishing (for contributors)

Both distribution targets are built from the same project.

```bash
# Pack as dotnet tool
dotnet pack -c Release

# Self-contained executables
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

Relevant `.csproj` properties:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>apix</ToolCommandName>
  <PackageId>apix</PackageId>
  <Version>1.0.0</Version>
  <RollForward>Major</RollForward>
</PropertyGroup>
```

---

## Demo Flow (Hackathon Presentation)

1. `apix config set editor` — configure preferred editor
2. `apix import` — register a live service from its Swagger URL
3. `apix service list` — show the registry
4. `apix endpoints list` — explore parsed endpoints
5. `apix call` — call a GET endpoint with a path param, show `Saved as #N` footer
6. `apix call` — call a POST endpoint, trigger body builder in editor, send
7. `apix call --verbose` — show full request and response headers
8. `apix call --full` — show a large truncated response in full
9. `apix history` — list recent requests
10. `apix history <id>` — inspect full request and response for a past call
11. `apix history <id> --curl` — export as curl command
12. `apix replay` — replay a previous request
13. `apix service update` — re-fetch schema, show diff of added/removed endpoints

---

## Testing with `dotnet run`

Run from `src/apix/` during development. Replace `apix <command>` with `dotnet run -- <command>`.

### Help

```bash
dotnet run -- --help
dotnet run -- import --help
dotnet run -- service --help
dotnet run -- endpoints --help
dotnet run -- call --help
dotnet run -- auth --help
dotnet run -- history --help
dotnet run -- replay --help
dotnet run -- config --help
```

### `import`

```bash
# from a URL
dotnet run -- import --name petstore --base-url https://petstore3.swagger.io --schema https://petstore3.swagger.io/api/v3/openapi.json

# from a local file (samples/petstore.json at repo root)
dotnet run -- import --name petstore --base-url https://petstore.example.com --schema ../../samples/petstore.json
```

### `service`

```bash
dotnet run -- service list

# remove (prompts for confirmation)
dotnet run -- service remove petstore

# update from URL (service must have been imported with a URL --schema)
dotnet run -- import --name petstore --base-url https://petstore3.swagger.io --schema https://petstore3.swagger.io/api/v3/openapi.json
dotnet run -- service update petstore

# update from local file
dotnet run -- service update petstore --file ../../samples/petstore.json
```

### `endpoints`

```bash
dotnet run -- endpoints list orderService
dotnet run -- endpoints list orderService --method GET
dotnet run -- endpoints list orderService --tag Orders
```

### `call`

```bash
dotnet run -- call orderService get-order --param orderId=abc-123
dotnet run -- call orderService list-orders --param page=1 --param pageSize=20
dotnet run -- call orderService create-order --body '{"symbol":"BTC","quantity":1,"side":"Buy"}'
dotnet run -- call orderService create-order --body @./order.json
dotnet run -- call orderService list-orders --full
dotnet run -- call orderService get-order --param orderId=abc-123 --verbose
dotnet run -- call orderService get-order --param orderId=abc-123 --no-save
```

### `auth`

```bash
dotnet run -- auth set orderService --bearer <token>
dotnet run -- auth remove orderService
```

### `history`

```bash
dotnet run -- history orderService
dotnet run -- history orderService 4
dotnet run -- history orderService 4 --verbose
dotnet run -- history orderService 4 --full
dotnet run -- history orderService 4 --request-only
dotnet run -- history orderService 4 --response-only
dotnet run -- history orderService 4 --curl
```

### `replay`

```bash
dotnet run -- replay orderService 3
```

### `config`

```bash
dotnet run -- config list
dotnet run -- config get editor
dotnet run -- config set editor "code --wait"
dotnet run -- config set editor vim
dotnet run -- config set editor notepad
dotnet run -- config unset editor
```
