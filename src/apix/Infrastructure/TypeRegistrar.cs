using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace apix.Infrastructure;

public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());
    public void Register(Type service, Type impl) => services.AddSingleton(service, impl);
    public void RegisterInstance(Type service, object impl) => services.AddSingleton(service, impl);
    public void RegisterLazy(Type service, Func<object> factory) => services.AddSingleton(service, _ => factory());
}

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);
    public void Dispose() { if (provider is IDisposable d) d.Dispose(); }
}
