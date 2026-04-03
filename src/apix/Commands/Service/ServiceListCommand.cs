using System.ComponentModel;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Service;

public class ServiceListCommand : AsyncCommand<ServiceListCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-q|--quiet")]
        [Description("Output only raw data, one item per line (pipe-friendly)")]
        public bool Quiet { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var entries = await ServiceRegistry.LoadAllAsync();

        if (entries.Count == 0)
        {
            if (settings.Quiet)
                return 0;
            
            AnsiConsole.MarkupLine("No services registered yet.");
            AnsiConsole.MarkupLine("Run [grey][[apix import]][/] to add your first service.");
            return 0;
        }

        if (settings.Quiet)
        {
            foreach (var entry in entries)
                Console.WriteLine($"{entry.Name}\t{entry.BaseUrl}\t{entry.EndpointCount}");
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
