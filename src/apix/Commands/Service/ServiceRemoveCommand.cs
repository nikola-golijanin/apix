using System.ComponentModel;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Service;

public class ServiceRemoveCommand : AsyncCommand<ServiceRemoveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name of the service to remove")]
        public required string Name { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var entry = await ServiceRegistry.FindAsync(settings.Name);
        if (entry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Service not found: [grey]{settings.Name}[/]");
            AnsiConsole.MarkupLine($"    [grey]→ Run [[apix service list]] to see registered services.[/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        var confirmed = AnsiConsole.Confirm($"Remove [cyan]{settings.Name}[/] and all its history?", defaultValue: false);
        AnsiConsole.WriteLine();

        if (!confirmed)
            return 0;

        await ServiceRegistry.RemoveAsync(settings.Name);
        AnsiConsole.MarkupLine($"  [green]✓[/] [cyan]{settings.Name}[/] removed.");
        return 0;
    }
}
