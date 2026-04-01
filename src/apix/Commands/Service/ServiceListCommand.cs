using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Service;

public class ServiceListCommand : AsyncCommand<ServiceListCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var entries = await ServiceRegistry.LoadAllAsync();

        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("No services registered yet.");
            AnsiConsole.MarkupLine("Run [grey][[apix import]][/] to add your first service.");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Base URL");
        table.AddColumn("Endpoints");

        foreach (var entry in entries)
            table.AddRow(entry.Name, entry.BaseUrl, entry.EndpointCount.ToString());

        AnsiConsole.Write(table);
        return 0;
    }
}
