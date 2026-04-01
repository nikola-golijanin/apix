using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class HistoryCommand : Command<HistoryCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]history[/]: hello world");
        return 0;
    }
}
