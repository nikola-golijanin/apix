using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class ReplayCommand : Command<ReplayCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]replay[/]: hello world");
        return 0;
    }
}
