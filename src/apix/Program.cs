using apix.Commands;
using apix.Commands.Auth;
using apix.Commands.Endpoints;
using apix.Commands.Service;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("apix");

    config.AddCommand<ImportCommand>("import")
          .WithDescription("Register a service from a local file or remote URL");

    config.AddBranch("service", service =>
    {
        service.AddCommand<ServiceListCommand>("list")
               .WithDescription("List registered services");
        service.AddCommand<ServiceRemoveCommand>("remove")
               .WithDescription("Unregister a service");
        service.AddCommand<ServiceUpdateCommand>("update")
               .WithDescription("Refresh a service schema");
    });

    config.AddBranch("endpoints", endpoints =>
    {
        endpoints.AddCommand<EndpointsListCommand>("list")
                 .WithDescription("List endpoints for a service");
    });

    config.AddCommand<CallCommand>("call")
          .WithDescription("Execute an HTTP request");

    config.AddBranch("auth", auth =>
    {
        auth.AddCommand<AuthSetCommand>("set")
            .WithDescription("Store a Bearer token for a service");
        auth.AddCommand<AuthRemoveCommand>("remove")
            .WithDescription("Remove a stored Bearer token");
    });

    config.AddCommand<HistoryCommand>("history")
          .WithDescription("View past requests");

    config.AddCommand<ReplayCommand>("replay")
          .WithDescription("Re-execute a previous request");

    config.AddCommand<ConfigCommand>("config")
          .WithDescription("View or set global configuration");
});

return app.Run(args);
