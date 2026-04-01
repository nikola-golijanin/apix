using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Service;

public class ServiceListCommand : Command<ServiceListCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]service list[/]: hello world");
        return 0;
    }
}
