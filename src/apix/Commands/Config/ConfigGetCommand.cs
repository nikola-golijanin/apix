using System.ComponentModel;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Config;

public class ConfigGetCommand : AsyncCommand<ConfigGetCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<key>")]
        [Description("Config key to retrieve (e.g. editor)")]
        public required string Key { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        switch (settings.Key.ToLowerInvariant())
        {
            case "editor":
                var stored = await ConfigService.GetEditorAsync();
                PrintEditor(stored);
                return 0;

            default:
                AnsiConsole.MarkupLine($"  [red]✕[/] Unknown config key: [white]{Markup.Escape(settings.Key)}[/]");
                AnsiConsole.MarkupLine($"    [grey]→ Supported keys: editor[/]");
                return 1;
        }
    }

    internal static void PrintEditor(string? stored)
    {
        if (stored is null)
        {
            var fallback = OperatingSystem.IsWindows() ? "notepad" : "nano";
            AnsiConsole.MarkupLine($"  editor    [grey](not set — using $EDITOR or {fallback})[/]");
        }
        else
        {
            var expanded = EditorService.ExpandPreset(stored);
            var isPreset = EditorService.KnownPresets.Contains(stored, StringComparer.OrdinalIgnoreCase);
            var label = isPreset ? "" : " [grey](custom)[/]";
            var arrow = string.Equals(expanded, stored, StringComparison.OrdinalIgnoreCase)
                ? ""
                : $"  [grey]→  {Markup.Escape(expanded)}[/]";
            AnsiConsole.MarkupLine($"  editor    [white]{Markup.Escape(stored)}[/]{label}{arrow}");
        }
    }
}
