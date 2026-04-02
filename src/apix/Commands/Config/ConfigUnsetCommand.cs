using System.ComponentModel;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Config;

public class ConfigUnsetCommand : AsyncCommand<ConfigUnsetCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<key>")]
        [Description("Config key to unset (e.g. editor)")]
        public required string Key { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        switch (settings.Key.ToLowerInvariant())
        {
            case "editor":
                await ConfigService.UnsetEditorAsync();
                AnsiConsole.MarkupLine("  [green]✔[/] editor unset [grey](will use $EDITOR or system default)[/]");
                return 0;

            default:
                AnsiConsole.MarkupLine($"  [red]✕[/] Unknown config key: [white]{Markup.Escape(settings.Key)}[/]");
                AnsiConsole.MarkupLine($"    [grey]→ Supported keys: editor[/]");
                return 1;
        }
    }
}
