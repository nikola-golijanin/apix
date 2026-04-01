using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Service;

public class ServiceRemoveCommand : Command<ServiceRemoveCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]service remove[/]: hello world");
        return 0;
    }
}
