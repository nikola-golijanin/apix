using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class CallCommand : Command<CallCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]call[/]: hello world");
        return 0;
    }
}
