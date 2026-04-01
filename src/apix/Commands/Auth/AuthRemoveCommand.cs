using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Auth;

public class AuthRemoveCommand : Command<AuthRemoveCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]auth remove[/]: hello world");
        return 0;
    }
}
