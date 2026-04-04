# Architecture Overview

## Tech stack

| Component | Package | Purpose |
|---|---|---|
| CLI framework | `Spectre.Console.Cli 1.0.0-alpha.0.15` | Command parsing, routing, terminal UI |
| Terminal output | `Spectre.Console 0.54.0` | Markup, tables, status spinners |
| OpenAPI parsing | `Microsoft.OpenApi.Readers 2.0.0-preview` | Parse OpenAPI 2.x/3.x schemas |
| HTTP client | `Microsoft.Extensions.Http 10.0.5` | `IHttpClientFactory` for all outbound requests |
| JSON | `System.Text.Json` (BCL) | Serialization/deserialization of all persisted data |
| Runtime | .NET 10 | |

---

## Project layout

```
src/apix/
├── Program.cs                  # Entry point, DI setup, command registration
├── Infrastructure/
│   └── TypeRegistrar.cs        # Bridges Spectre.Cli with Microsoft.Extensions.DI
├── Commands/
│   ├── ImportCommand.cs
│   ├── CallCommand.cs
│   ├── HistoryCommand.cs
│   ├── ReplayCommand.cs
│   ├── Auth/
│   ├── Config/
│   ├── Endpoint/
│   ├── Open/
│   └── Service/
├── Services/
│   ├── ServiceRegistry.cs      # Service registry + schema file I/O
│   ├── HistoryService.cs       # Request/response history persistence
│   ├── EditorService.cs        # Template generation + editor process launch
│   └── ConfigService.cs        # ~/.apix/config.json read/write
├── Helpers/
│   ├── OutputHelpers.cs        # Console formatting (separators, body, age)
│   ├── RequestHelpers.cs       # JSON template parsing + URL construction
│   └── StringHelpers.cs        # Levenshtein fuzzy matching
└── Models/
    ├── ServiceEntry.cs
    ├── SchemaSource.cs
    └── HistoryEntry.cs
```

---

## Dependency injection

Spectre.Console.Cli has its own type resolution system. The `TypeRegistrar` / `TypeResolver` pair in `Infrastructure/` bridges it with `Microsoft.Extensions.DependencyInjection`.

**Setup in `Program.cs`:**

```csharp
var services = new ServiceCollection();
services.AddHttpClient();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
```

Commands that require `IHttpClientFactory` declare it via constructor injection and are resolved through the registrar automatically:

```csharp
public class CallCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<...>
```

All services (`ServiceRegistry`, `HistoryService`, `ConfigService`, `EditorService`) are **static classes** — they require no DI registration and are called directly.

---

## Command registration

Commands are registered in `Program.cs` using Spectre.Console.Cli's fluent API:

```
apix import                     → ImportCommand
apix service list               → ServiceListCommand
apix service remove             → ServiceRemoveCommand
apix service update             → ServiceUpdateCommand
apix endpoint list              → EndpointListCommand
apix endpoint details           → EndpointDetailCommand
apix call                       → CallCommand
apix auth set                   → AuthSetCommand  (stub)
apix auth remove                → AuthRemoveCommand  (stub)
apix history                    → HistoryCommand
apix replay                     → ReplayCommand
apix open [default]             → OpenCommand  (via SetDefaultCommand)
apix open history               → OpenHistoryCommand
apix open replay                → OpenReplayCommand
apix config set                 → ConfigSetCommand
apix config get                 → ConfigGetCommand
apix config list                → ConfigListCommand
apix config unset               → ConfigUnsetCommand
```

The `open` branch uses `SetDefaultCommand<OpenCommand>()` so that `apix open --url <url>` routes to `OpenCommand` while `apix open history` and `apix open replay` route to their respective sub-commands.

---

## Local storage layout

All state lives under `~/.apix/`:

```
~/.apix/
├── services.json               # Registry index (name, baseUrl, schemaSource, endpointCount, importedAt)
├── schemas/
│   └── <name>.json             # Raw OpenAPI JSON per service
├── history/
│   ├── <service>.json          # Per-service request/response history
│   └── _open.json              # History for apix open requests (global, no service)
├── config.json                 # { "editor": "vscode" }
└── auth.json                   # Not yet implemented
```

### JSON serialization options

All services use the same `JsonSerializerOptions`:

```csharp
new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}
```

---

## Common command patterns

Every implemented command follows this structure:

1. **Validate inputs** — resolve service from registry, check IDs exist, validate URLs
2. **Load data** — read schema stream, deserialize history, etc.
3. **Do work** — parse, execute HTTP, build template, open editor
4. **Print output** — use `OutputHelpers` for consistent formatting
5. **Return 0 on success, 1 on any error**

### Fuzzy match on not-found errors

Commands that look up a service or operation ID use `StringHelpers.FindClosestMatch` to suggest the closest known name:

```csharp
var suggestion = StringHelpers.FindClosestMatch(settings.Service, allNames);
AnsiConsole.MarkupLine(suggestion is not null
    ? $"    [grey]→ Did you mean: [white]{suggestion}[/]?[/]"
    : $"    [grey]→ Run [[apix service list]] to see registered services.[/]");
```

### Status spinner

Long-running I/O (HTTP fetches, schema parsing) is wrapped in `AnsiConsole.Status().StartAsync(...)` to show a spinner:

```csharp
await AnsiConsole.Status().StartAsync("Fetching schema…", async ctx => { ... });
```
