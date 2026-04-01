using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Endpoints;

public class EndpointsListCommand : Command<EndpointsListCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]endpoints list[/]: hello world");
        return 0;
    }
}
