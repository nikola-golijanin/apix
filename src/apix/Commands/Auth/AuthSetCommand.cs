using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Auth;

public class AuthSetCommand : Command<AuthSetCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]auth set[/]: hello world");
        return 0;
    }
}
