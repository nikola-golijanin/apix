using System.ComponentModel;
using apix.Services;
using Microsoft.OpenApi.Reader;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Endpoint;

public class EndpointListCommand : AsyncCommand<EndpointListCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<service>")]
        [Description("Name of the registered service")]
        public required string Service { get; init; }

        [CommandOption("-m|--method <method>")]
        [Description("Filter by HTTP method (GET, POST, PUT, DELETE, PATCH)")]
        public string? Method { get; init; }

        [CommandOption("-t|--tag <tag>")]
        [Description("Filter by tag")]
        public string? Tag { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var entry = await ServiceRegistry.FindAsync(settings.Service);
        if (entry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Service not found: [grey]{settings.Service}[/]");
            AnsiConsole.MarkupLine($"    [grey]→ Run [[apix service list]] to see registered services.[/]");
            return 1;
        }

        await using var schemaStream = ServiceRegistry.OpenSchema(settings.Service);
        var reader = new OpenApiJsonReader();
        var result = await reader.ReadAsync(schemaStream, new OpenApiReaderSettings(), cancellationToken);

        if (result.Document is not { } document)
        {
            AnsiConsole.MarkupLine("  [red]✕[/] Failed to read stored schema.");
            return 1;
        }

        var methodFilter = settings.Method?.ToUpperInvariant();
        var tagFilter = settings.Tag;

        var operations = document.Paths?
            .Where(path => path.Value.Operations is not null)
            .SelectMany(path => path.Value.Operations!.Select(op => (
                Method: op.Key.ToString().ToUpperInvariant(),
                Path: path.Key,
                OperationId: op.Value.OperationId ?? "",
                Tags: op.Value.Tags?.Select(t => t.Name).ToList() ?? []
            )))
            .Where(op => methodFilter is null || op.Method == methodFilter)
            .Where(op => tagFilter is null || op.Tags.Contains(tagFilter, StringComparer.OrdinalIgnoreCase))
            .ToList() ?? [];

        var header = $"[cyan]{entry.Name}[/] — [grey]{entry.BaseUrl}[/]";
        if (methodFilter is not null)
            header += $"  [grey][[{methodFilter}]][/]";
        if (tagFilter is not null)
            header += $"  [grey][[{tagFilter}]][/]";

        if (operations.Count == 0)
        {
            AnsiConsole.MarkupLine(header);
            AnsiConsole.MarkupLine(new string('─', 66));
            AnsiConsole.MarkupLine("  [grey]No endpoints match the given filters.[/]");
            return 0;
        }

        var verbWidth  = 7; // DELETE is the longest standard verb
        var routeWidth = Math.Max("Route".Length, operations.Max(op => op.Path.Length));
        var opIdWidth  = Math.Max("Operation ID".Length, operations.Max(op => op.OperationId.Length));
        var tagWidth   = Math.Max("Tag".Length, operations.Max(op => string.Join(", ", op.Tags).Length));
        var lineWidth  = 2 + verbWidth + 2 + routeWidth + 2 + opIdWidth + 2 + tagWidth;

        AnsiConsole.MarkupLine(header);
        AnsiConsole.MarkupLine(new string('─', lineWidth));
        AnsiConsole.MarkupLine($"  [grey]{"Verb".PadRight(verbWidth)}  {"Route".PadRight(routeWidth)}  {"Operation ID".PadRight(opIdWidth)}  Tag[/]");
        AnsiConsole.MarkupLine(new string('─', lineWidth));

        foreach (var (method, path, operationId, tags) in operations)
        {
            var methodColor = method switch
            {
                "GET"    => "green",
                "POST"   => "blue",
                "PUT"    => "yellow",
                "DELETE" => "red",
                "PATCH"  => "magenta",
                _        => "white"
            };
            var tagDisplay = string.Join(", ", tags);
            AnsiConsole.MarkupLine($"  [{methodColor}]{method.PadRight(verbWidth)}[/]  [white]{path.PadRight(routeWidth)}[/]  [grey]{operationId.PadRight(opIdWidth)}  {tagDisplay}[/]");
        }

        return 0;
    }
}
