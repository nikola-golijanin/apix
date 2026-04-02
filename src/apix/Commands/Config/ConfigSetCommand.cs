using System.ComponentModel;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Config;

public class ConfigSetCommand : AsyncCommand<ConfigSetCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<key>")]
        [Description("Config key to set (e.g. editor)")]
        public required string Key { get; init; }

        [CommandArgument(1, "<value>")]
        [Description("Value to assign")]
        public required string Value { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        switch (settings.Key.ToLowerInvariant())
        {
            case "editor":
                await ConfigService.SetEditorAsync(settings.Value);
                var expanded = EditorService.ExpandPreset(settings.Value);
                AnsiConsole.MarkupLine($"  [green]✔[/] editor set to [white]{Markup.Escape(settings.Value)}[/]");
                if (!string.Equals(expanded, settings.Value, StringComparison.OrdinalIgnoreCase))
                    AnsiConsole.MarkupLine($"    [grey]→ launches as: {Markup.Escape(expanded)}[/]");
                return 0;

            default:
                AnsiConsole.MarkupLine($"  [red]✕[/] Unknown config key: [white]{Markup.Escape(settings.Key)}[/]");
                AnsiConsole.MarkupLine($"    [grey]→ Supported keys: editor[/]");
                return 1;
        }
    }
}
