using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;

namespace apix.Helpers;

internal static class OutputHelpers
{
    private const int TruncateLines = 40;

    public static void Separator(int width) =>
        AnsiConsole.MarkupLine($"[grey]{new string('─', width)}[/]");

    public static void PrintHeaders(Dictionary<string, string> headers)
    {
        if (headers.Count == 0) return;
        var pad = headers.Keys.Max(k => k.Length) + 4;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [grey]Headers:[/]");
        foreach (var (k, v) in headers)
            AnsiConsole.MarkupLine($"  [grey]  {k.PadRight(pad)}{v}[/]");
    }

    /// <summary>Prints a body block. Returns true if the body was truncated.</summary>
    public static bool PrintBody(string body, bool full)
    {
        string display;
        try
        {
            var node = JsonNode.Parse(body);
            display = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? body;
        }
        catch
        {
            display = body;
        }

        var lines = display.Split('\n');
        var truncated = !full && lines.Length > TruncateLines;
        var printLines = truncated ? lines.Take(TruncateLines) : lines;

        foreach (var line in printLines)
            AnsiConsole.WriteLine("  " + line);

        return truncated;
    }

    public static string FormatAge(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;
        return elapsed.TotalSeconds switch
        {
            < 60    => "just now",
            < 3600  => $"{(int)elapsed.TotalMinutes} min ago",
            < 86400 => $"{(int)elapsed.TotalHours} hr ago",
            _       => $"{(int)elapsed.TotalDays} days ago"
        };
    }
}
