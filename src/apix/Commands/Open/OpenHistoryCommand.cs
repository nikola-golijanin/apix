using System.ComponentModel;
using System.Text;
using apix.Helpers;
using apix.Models;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Open;

public class OpenHistoryCommand : AsyncCommand<OpenHistoryCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[id]")]
        [Description("Entry ID to inspect (omit to list recent requests)")]
        public int? Id { get; init; }

        [CommandOption("-v|--verbose")]
        [Description("Show request/response headers and full response body")]
        public bool Verbose { get; init; }

        [CommandOption("--request-only")]
        [Description("Show only the request block")]
        public bool RequestOnly { get; init; }

        [CommandOption("--response-only")]
        [Description("Show only the response block")]
        public bool ResponseOnly { get; init; }

        [CommandOption("-c|--curl")]
        [Description("Print the request as a curl command")]
        public bool Curl { get; init; }

        [CommandOption("-a|--all")]
        [Description("Show all history entries (list mode only, default: last 20)")]
        public bool TakeAll { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        return settings.Id is null
            ? await ListAsync(settings)
            : await InspectAsync(settings);
    }

    // ── List mode ─────────────────────────────────────────────────────────────

    private static async Task<int> ListAsync(Settings settings)
    {
        var entries = await HistoryService.LoadAllAsync(OpenCommand.HistoryKey);

        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No open requests made yet.[/]");
            AnsiConsole.MarkupLine("  [grey]→ Run [[apix open <url>]] to get started.[/]");
            return 0;
        }

        var recent = settings.TakeAll
            ? [.. entries.OrderByDescending(e => e.Id)]
            : entries.OrderByDescending(e => e.Id).Take(20).ToList();

        var idWidth     = recent.Max(e => $"#{e.Id}".Length);
        var mthWidth    = Math.Max("Method".Length,   recent.Max(e => e.Method.Length));
        var statusWidth = Math.Max("Status".Length,   recent.Max(e => e.StatusCode.ToString().Length));
        var durWidth    = Math.Max("Duration".Length, recent.Max(e => $"{e.DurationMs}ms".Length));
        var whenWidth   = Math.Max("When".Length,     recent.Max(e => OutputHelpers.FormatAge(e.Timestamp).Length));

        var terminalWidth = AnsiConsole.Profile.Width;
        var fixedOverhead = 2 + idWidth + 2 + mthWidth + 2 + statusWidth + 2 + durWidth + 2 + whenWidth + 2;
        var urlMax        = Math.Max(10, terminalWidth - fixedOverhead);

        var urlWidth = Math.Min(
            Math.Max("URL".Length, recent.Max(e => e.Url.Length)),
            Math.Max("URL".Length, urlMax));

        var lineWidth = idWidth + 2 + mthWidth + 2 + urlWidth + 2 + statusWidth + 2 + durWidth + 2 + whenWidth;

        var countLabel = settings.TakeAll ? $"all {recent.Count}" : $"last {recent.Count}";
        AnsiConsole.MarkupLine($"[cyan]open[/] [grey]— {countLabel} requests[/]");
        OutputHelpers.Separator(lineWidth);
        AnsiConsole.MarkupLine(
            $"  [grey]{"#".PadRight(idWidth)}  {"Method".PadRight(mthWidth)}  {"URL".PadRight(urlWidth)}  " +
            $"{"Status".PadRight(statusWidth)}  {"Duration".PadRight(durWidth)}  When[/]");
        OutputHelpers.Separator(lineWidth);

        foreach (var e in recent)
        {
            var statusColor = StatusColor(e.StatusCode);
            var id       = Markup.Escape($"#{e.Id}".PadRight(idWidth));
            var method   = Markup.Escape(e.Method.PadRight(mthWidth));
            var url      = Markup.Escape(Trunc(e.Url, urlWidth).PadRight(urlWidth));
            var status   = Markup.Escape(e.StatusCode.ToString().PadRight(statusWidth));
            var duration = Markup.Escape($"{e.DurationMs}ms".PadRight(durWidth));
            var when     = Markup.Escape(OutputHelpers.FormatAge(e.Timestamp));

            AnsiConsole.MarkupLine(
                $"  [grey]{id}[/]  [white]{method}[/]  [grey]{url}[/]  " +
                $"[{statusColor}]{status}[/]  [grey]{duration}  {when}[/]");
        }

        return 0;
    }

    // ── Inspect mode ──────────────────────────────────────────────────────────

    private static async Task<int> InspectAsync(Settings settings)
    {
        var e = await HistoryService.FindAsync(OpenCommand.HistoryKey, settings.Id!.Value);
        if (e is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] No open request found with id [white]#{settings.Id}[/].");
            AnsiConsole.MarkupLine("    [grey]→ Run [[apix open history]] to see available requests.[/]");
            return 1;
        }

        if (settings.Curl)
        {
            PrintCurl(e);
            return 0;
        }

        var sepWidth = Math.Max(40, $"  {e.Method} {e.Url}".Length);
        var statusColor = StatusColor(e.StatusCode);
        var statusIcon  = e.StatusCode is >= 200 and < 400 ? "◆" : "✕";

        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  [grey]#{e.Id} — {e.Timestamp.LocalDateTime:dd MMM yyyy  HH:mm:ss}[/]");
        OutputHelpers.Separator(sepWidth);

        if (!settings.ResponseOnly)
        {
            AnsiConsole.MarkupLine("  [grey]REQUEST[/]");
            OutputHelpers.Separator(sepWidth);
            AnsiConsole.MarkupLine($"  {e.Method} {Markup.Escape(e.Url)}");

            if (settings.Verbose)
                OutputHelpers.PrintHeaders(e.RequestHeaders);

            if (e.RequestBody is not null)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("  [grey]Body:[/]");
                OutputHelpers.PrintBody(e.RequestBody, full: settings.Verbose);
            }

            if (!settings.RequestOnly)
                AnsiConsole.WriteLine();
        }

        if (settings.RequestOnly) 
            return 0;
        
        AnsiConsole.MarkupLine($"  [grey]RESPONSE[/]  [{statusColor}]{statusIcon} {e.StatusCode} {e.StatusText}[/]  [grey][[{e.DurationMs}ms]][/]");
        OutputHelpers.Separator(sepWidth);

        if (settings.Verbose)
            OutputHelpers.PrintHeaders(e.ResponseHeaders);

        if (e.ResponseBody is null) 
            return 0;
            
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [grey]Body:[/]");
            
        var truncated = OutputHelpers.PrintBody(e.ResponseBody, full: settings.Verbose);
        if (!truncated) 
            return 0;
            
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [grey]... lines truncated — rerun with --verbose to print in full[/]");

        return 0;
    }

    // ── Curl output ───────────────────────────────────────────────────────────

    private static void PrintCurl(HistoryEntry e)
    {
        var sb = new StringBuilder();
        sb.Append($"curl -X {e.Method} {e.Url}");

        foreach (var (k, v) in e.RequestHeaders)
            sb.Append($" \\\n  -H \"{k}: {v}\"");

        if (e.RequestBody is not null)
        {
            var compact = e.RequestBody.ReplaceLineEndings(" ");
            sb.Append($" \\\n  -d '{compact}'");
        }

        AnsiConsole.WriteLine(sb.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string StatusColor(int code) => code switch
    {
        >= 200 and < 300 => "green",
        >= 300 and < 400 => "cyan",
        >= 400 and < 500 => "yellow",
        _                => "red"
    };

    private static string Trunc(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
