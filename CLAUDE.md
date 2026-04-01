# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**apix** is a .NET 10 CLI tool — a terminal-native alternative to Postman for developers managing multiple microservices. It imports OpenAPI schemas, maintains a local service registry, executes HTTP requests, and stores request/response history.

## Build & Run

All commands run from `src/apix/`:

```bash
dotnet restore
dotnet build
dotnet run -- <command> [args]   # e.g. dotnet run -- import --help

# Pack as a dotnet global tool
dotnet pack -c Release

# Self-contained single-file binaries
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

There is no test project yet. Manual testing uses `dotnet run` with the sample schema at `samples/petstore.json`.

### Installing / updating the global tool locally

After making code changes, reinstall the global tool to test it as `apix` instead of `dotnet run --`:

```bash
dotnet pack -c Release
dotnet tool update --global apix --add-source ./bin/Release
```

First-time install (if not yet installed):

```bash
dotnet tool install --global apix --add-source ./bin/Release
```

Run these from `src/apix/`. The `--add-source` flag points to the local nupkg instead of NuGet.

## Architecture

### Command structure (Spectre.Console.Cli)

Each CLI command is a class inheriting from `Command<T>` or `AsyncCommand<T>` with a nested `Settings` class that declares options via `[CommandOption]` attributes. Commands are registered in `Program.cs` via `app.AddCommand<T>()` and `app.AddBranch()`.

Current commands:
- `import` — Fully implemented. Fetches an OpenAPI schema from a URL or local file path, parses it with `Microsoft.OpenApi.Readers`, and displays info.
- `call`, `config`, `history`, `replay` — Stub implementations.
- Sub-branches: `service` (list/remove/update), `endpoints list`, `auth` (set/remove) — All stubs.

### Dependency injection bridge

`Infrastructure/TypeRegistrar.cs` implements Spectre.Cli's `ITypeRegistrar` interface to bridge it with `Microsoft.Extensions.DependencyInjection`. This is wired up in `Program.cs` so commands receive injected services via constructor injection.

### Data persistence

All state is stored as JSON files under `~/.apix/`:
- `services.json` — registry index (name, baseUrl, schemaSource, endpointCount, importedAt)
- `schemas/<name>.json` — raw OpenAPI JSON per service
- `history/<service>.json` — per-service request/response history (not yet implemented)
- `auth.json` — Bearer tokens per service (not yet implemented)
- `config.json` — global config (not yet implemented)

`Services/ServiceRegistry.cs` owns all reads and writes. Its methods are static — no DI registration needed.

Commands that need schema data follow this pattern: `ServiceRegistry.FindAsync(name)` → `ServiceRegistry.OpenSchema(name)` → parse with `OpenApiJsonReader` → work with `OpenApiDocument`.

### Key dependencies

| Package | Purpose |
|---|---|
| `Spectre.Console` / `Spectre.Console.Cli` | Terminal UI + command parsing |
| `Microsoft.OpenApi.Readers` | Parse OpenAPI 2.x/3.x schemas |
| `Microsoft.Extensions.Http` | `IHttpClientFactory` |

## Implementation Status

See `docs/proposal.md` for the full spec — commands marked ✅ are implemented. Consult it before implementing any new command.
