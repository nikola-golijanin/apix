# TypeRegistrar / TypeResolver

**File:** `src/apix/Infrastructure/TypeRegistrar.cs`

---

## Responsibility

Bridges Spectre.Console.Cli's `ITypeRegistrar` / `ITypeResolver` interfaces with `Microsoft.Extensions.DependencyInjection`, enabling constructor injection in command classes.

---

## Classes

### `TypeRegistrar` (implements `ITypeRegistrar`)

Wraps an `IServiceCollection`. Spectre.Console.Cli calls the registration methods during its internal wiring phase.

| Method | Maps to |
|---|---|
| `Register(Type service, Type impl)` | `services.AddSingleton(service, impl)` |
| `RegisterInstance(Type service, object impl)` | `services.AddSingleton(service, impl)` |
| `RegisterLazy(Type service, Func<object> factory)` | `services.AddSingleton(service, _ => factory())` |
| `Build()` | Builds the `ServiceProvider`, wraps it in `TypeResolver` |

### `TypeResolver` (implements `ITypeResolver`, `IDisposable`)

Wraps the built `IServiceProvider`. Spectre.Console.Cli calls `Resolve` to instantiate command classes.

| Method | Behavior |
|---|---|
| `Resolve(Type? type)` | `provider.GetService(type)` — returns `null` for unknown types |
| `Dispose()` | Disposes the underlying `IServiceProvider` if it implements `IDisposable` |

---

## Wiring in `Program.cs`

```csharp
var services = new ServiceCollection();
services.AddHttpClient();
// ... register other services ...

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
```

This pattern allows commands like `CallCommand` to declare `IHttpClientFactory` as a constructor parameter and receive it automatically.

---

## Static commands

`ServiceRegistry`, `HistoryService`, `EditorService`, and `ConfigService` are all `static` classes — they do not need DI registration and are called directly.
