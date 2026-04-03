# Usage

## Core concepts

- **Service** — a named entry in the local registry, consisting of a name, base URL, and imported OpenAPI schema
- **Endpoint** — an individual HTTP operation parsed from the schema (method + path + operationId)
- **Request** — an executed HTTP call, stored in local history per service
- **Body** — for POST/PUT/PATCH requests, constructed interactively via `$EDITOR`

## Typical workflow

1. **Import** a service from its OpenAPI schema URL or a local file
2. **List endpoints** to explore what operations are available
3. **Call** an endpoint — apix opens your editor for any parameters or request body
4. **Inspect history** to review past requests and responses
5. **Replay** a previous request, optionally editing it first

## Commands

### import

Register a service by importing its OpenAPI schema.

```
apix import --name <name> --base-url <url> --schema <path|url>
```

**Options**

| Flag | Description |
|---|---|
| `--name` | Local name for the service |
| `--base-url` | Base URL for all API requests |
| `--schema` | URL or local file path to the OpenAPI schema |

**Examples**

```
apix import --name petstore --base-url https://petstore3.swagger.io --schema https://petstore3.swagger.io/api/v3/openapi.json
apix import --name orderService --base-url https://api.orders.internal --schema ./openapi.json
```

**Output**

```
Importing petstore...

  ✓ Schema loaded     (OpenAPI 3.0.1)
  ✓ 19 endpoints parsed
  ✓ Saved to registry

petstore is ready. Run [apix endpoint list petstore] to explore.
```

---

### service list

Show all registered services.

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
└──────────────────┴────────────────────────────────────┴───────────┘
```

---

### service remove

Unregister a service and delete its history.

```
apix service remove <name>
```

apix prompts for confirmation before removing.

---

### service update

Refresh the schema for a registered service.

```
apix service update <name>
apix service update <name> --file <path>
```

**Options**

| Flag | Description |
|---|---|
| `--file` | Provide a new local schema file instead of re-fetching from the original URL |

When the service was imported from a URL, `service update` re-fetches it automatically. Use `--file` when the service was imported from a local file or when you want to point to a different schema.

**Output**

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

---

### endpoint list

List all endpoints for a service, with optional filters.

```
apix endpoint list <name>
apix endpoint list <name> --method GET
apix endpoint list <name> --tag Orders
```

**Options**

| Flag | Description |
|---|---|
| `--method` | Filter by HTTP method (GET, POST, PUT, DELETE, PATCH) |
| `--tag` | Filter by OpenAPI tag |

**Output**

```
orderService — https://api.orders.internal
──────────────────────────────────────────────────────────────────────────────
  Verb     Route                    Operation ID      Tag
──────────────────────────────────────────────────────────────────────────────
  GET      /orders/{orderId}        get-order         orders
  POST     /orders                  create-order      orders
  DELETE   /orders/{orderId}        cancel-order      orders
  GET      /orders                  list-orders       orders
```

HTTP methods are color-coded: `GET` green · `POST` blue · `PUT` yellow · `DELETE` red · `PATCH` magenta.

---

### call

Execute a request by operation ID.

```
apix call <service> <operationId>
apix call <service> <operationId> --verbose
apix call <service> <operationId> --no-save
```

**Options**

| Flag | Description |
|---|---|
| `-v, --verbose` | Print full request/response headers and full response body |
| `--no-save` | Execute without saving to history |

**Examples**

```
apix call petstore getPetById
apix call orderService create-order --verbose
apix call orderService get-order --no-save
```

When the operation has path parameters, query parameters, headers, or a request body, apix opens your editor with a pre-filled JSON template (see [Editor template workflow](#editor-template-workflow) below). Operations with no parameters execute immediately.

**Output**

```
──────────────────────────────────────────────────────────
  REQUEST
──────────────────────────────────────────────────────────
  POST https://api.orders.internal/orders

──────────────────────────────────────────────────────────
  RESPONSE  ◆ 201 Created  [143ms]
──────────────────────────────────────────────────────────

  Body:
  {
    "orderId": "abc-123",
    "status": "pending"
  }

  Saved as #4 — [apix history orderService 4] to inspect · [apix replay orderService 4] to replay
```

---

### history

View and inspect past requests for a service.

**List recent requests**

```
apix history <service>
apix history <service> --all
```

**Output**

```
orderService — last 20 requests
──────────────────────────────────────────────────────────────────────────────
  #   Method   Path                    Status   Duration   Operation     When
──────────────────────────────────────────────────────────────────────────────
  #1  POST     /orders                 201       143ms     create-order  2 min ago
  #2  GET      /orders/abc-123         200        87ms     get-order     2 min ago
  #3  GET      /orders/999             404        61ms     get-order     1 min ago
```

Status codes are color-coded: `2xx` green · `3xx` cyan · `4xx` yellow · `5xx` red.

**Inspect a specific request**

```
apix history <service> <id>
apix history <service> <id> --verbose
apix history <service> <id> --request-only
apix history <service> <id> --response-only
apix history <service> <id> --curl
```

**Options**

| Flag | Description |
|---|---|
| `-a, --all` | Show all history entries (default: last 20) |
| `-v, --verbose` | Show headers and full response body |
| `--request-only` | Print only the request block |
| `--response-only` | Print only the response block |
| `-c, --curl` | Print the request as a curl command |

**`--curl` output**

```
curl -X POST https://api.orders.internal/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTC","quantity":1,"side":"Buy"}'
```

---

### replay

Re-execute a previous request.

```
apix replay <service> <id>
apix replay <service> <id> --edit
apix replay <service> <id> --no-save
apix replay <service> <id> --verbose
```

**Options**

| Flag | Description |
|---|---|
| `-e, --edit` | Open editor pre-filled with stored request values before sending |
| `--no-save` | Execute without saving to history |
| `-v, --verbose` | Print full request/response headers and full response body |

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

### config

Manage global tool configuration.

```
apix config set <key> <value>
apix config get <key>
apix config list
apix config unset <key>
```

**Examples**

```
apix config set editor vscode
apix config get editor
apix config list
apix config unset editor
```

**`config list` output**

```
Config
────────────────────────────────────────
  editor    vscode   →  code --wait

  Presets: vim · nano · vscode · notepad
  Run [apix config set editor <preset>] to change.
```

---

## Editor template workflow

When `call` or `replay --edit` needs input, apix opens your editor with a pre-filled JSON template. Only sections relevant to the operation are included:

```json
{
  "path": {
    "orderId": "string"
  },
  "query": {
    "page": "integer?"
  },
  "headers": {
    "X-Correlation-Id": "string?"
  },
  "body": {
    "symbol": "string",
    "quantity": "integer",
    "side": "Buy | Sell",
    "clientOrderId": "string?"
  }
}
```

Required fields show the type name (`string`, `integer`). Optional fields are marked with `?`. Enum values are listed inline. Fill in your values, save, and close the editor — apix sends the request.

**Configuring your editor**

The editor is resolved in this order:

1. `apix config set editor` value (if set)
2. `$EDITOR` environment variable (if set)
3. Falls back to `nano` on Linux/macOS, `notepad` on Windows

```
apix config set editor vscode   # expands to: code --wait
apix config set editor vim
apix config set editor nano
apix config set editor "my-editor --wait"
```

---

## Data storage

All data is stored locally. No telemetry, no remote sync.

| Data | Location |
|---|---|
| Service registry | `~/.apix/services.json` |
| Cached schemas | `~/.apix/schemas/<name>.json` |
| Request history | `~/.apix/history/<service>.json` |
| Global config | `~/.apix/config.json` |
| Body templates (temp) | System temp directory, deleted after send |
