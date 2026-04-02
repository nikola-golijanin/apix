using apix.Helpers;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Config;

public class ConfigListCommand : AsyncCommand<ConfigListCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var stored = await ConfigService.GetEditorAsync();

        AnsiConsole.MarkupLine("[cyan]Config[/]");
        OutputHelpers.Separator(40);
        ConfigGetCommand.PrintEditor(stored);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [grey]Presets: {string.Join(" · ", EditorService.KnownPresets)}[/]");
        AnsiConsole.MarkupLine($"  [grey]Run [[apix config set editor <preset>]] to change.[/]");

        return 0;
    }
}
