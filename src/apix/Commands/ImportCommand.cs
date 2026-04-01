using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class ImportCommand : Command<ImportCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]import[/]: hello world");
        return 0;
    }
}
