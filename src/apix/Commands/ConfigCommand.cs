using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class ConfigCommand : Command<ConfigCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]config[/]: hello world");
        return 0;
    }
}
