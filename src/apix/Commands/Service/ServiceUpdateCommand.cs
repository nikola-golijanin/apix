using System.ComponentModel;
using apix.Helpers;
using apix.Models;
using apix.Services;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Service;

public class ServiceUpdateCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<ServiceUpdateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name of the registered service to update")]
        public required string Name { get; init; }

        [CommandOption("-f|--file <path>")]
        [Description("Path to a new local schema file")]
        public string? File { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var entry = await ServiceRegistry.FindAsync(settings.Name);
        if (entry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Service not found: [grey]{settings.Name}[/]");
            var allNames = (await ServiceRegistry.LoadAllAsync()).Select(e => e.Name);
            var suggestion = StringHelpers.FindClosestMatch(settings.Name, allNames);
            AnsiConsole.MarkupLine(suggestion is not null
                ? $"    [grey]→ Did you mean: [white]{Markup.Escape(suggestion)}[/]?[/]"
                : $"    [grey]→ Run [[apix service list]] to see registered services.[/]");
            return 1;
        }

        // Resolve source mode
        var isFileMode = settings.File is not null;

        if (!isFileMode && entry.SchemaSource.Type == SchemaSourceType.File)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Cannot update [cyan]{settings.Name}[/] — schema was imported from a local file and no URL is available.");
            AnsiConsole.MarkupLine($"    [grey]→ Provide a new schema file with --file:[/]");
            AnsiConsole.MarkupLine($"      [grey]apix service update {settings.Name} --file ./openapi.json[/]");
            return 1;
        }

        byte[]? newBytes = null;
        string? loadError = null;
        OpenApiDocument? newDoc = null;
        string? versionLabel = null;

        var spinnerLabel = isFileMode ? "Loading schema from file…" : "Fetching updated schema…";
        await AnsiConsole.Status()
            .StartAsync(spinnerLabel, async ctx =>
            {
                var (bytes, error) = await LoadNewBytesAsync(settings, entry, cancellationToken);
                if (error is not null) { loadError = error; return; }
                newBytes = bytes;

                ctx.Status("Parsing schema…");
                var reader = new OpenApiJsonReader();
                var newResult = await reader.ReadAsync(new MemoryStream(newBytes!), new OpenApiReaderSettings(), cancellationToken);
                if (newResult.Document is not { } doc) { loadError = "__parse_failed__"; return; }

                newDoc = doc;
                versionLabel = newResult.Diagnostic?.SpecificationVersion switch
                {
                    Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0 => "OpenAPI 2.0",
                    Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0 => "OpenAPI 3.0",
                    Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1 => "OpenAPI 3.1",
                    _ => "OpenAPI"
                };
            });

        if (loadError == "__parse_failed__")
        {
            AnsiConsole.MarkupLine("  [red]✕[/] Invalid OpenAPI schema — failed to parse document.");
            return 1;
        }
        if (loadError is not null)
        {
            AnsiConsole.MarkupLine(loadError);
            return 1;
        }

        AnsiConsole.MarkupLine(isFileMode
            ? $"  [green]✓[/] Schema loaded         [grey]({versionLabel})[/]"
            : $"  [green]✓[/] Schema re-fetched     [grey]({versionLabel})[/]");

        // Compute diff against old schema
        await using var oldStream = ServiceRegistry.OpenSchema(settings.Name);
        var diffReader = new OpenApiJsonReader();
        var oldResult = await diffReader.ReadAsync(oldStream, new OpenApiReaderSettings(), cancellationToken);
        var oldOps = ExtractOperations(oldResult.Document);
        var newOps = ExtractOperations(newDoc!);

        var added   = newOps.Where(n => !oldOps.Any(o => o.Method == n.Method && o.Path == n.Path)).ToList();
        var removed = oldOps.Where(o => !newOps.Any(n => n.Method == o.Method && n.Path == o.Path)).ToList();

        switch (added.Count)
        {
            case 0 when removed.Count == 0:
                AnsiConsole.MarkupLine("  [green]✓[/] No changes detected — registry is already up to date.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[cyan]{settings.Name}[/] is up to date.");
                return 0;
            case > 0:
            {
                AnsiConsole.MarkupLine($"  [green]✓[/] {added.Count} endpoint{(added.Count == 1 ? "" : "s")} added");
                foreach (var op in added)
                    AnsiConsole.MarkupLine($"    [green]+[/]  [green]{op.Method,-7}[/]  [white]{op.Path,-40}[/]  [grey]{op.OperationId}[/]");
                break;
            }
        }

        if (removed.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [green]✓[/] {removed.Count} endpoint{(removed.Count == 1 ? "" : "s")} removed");
            foreach (var op in removed)
                AnsiConsole.MarkupLine($"    [red]-[/]  [grey]{op.Method,-7}  {op.Path,-40}  {op.OperationId}[/]");
        }

        // Save updated entry
        var newEndpointCount = newOps.Count;
        var newSource = isFileMode
            ? new SchemaSource(SchemaSourceType.File, Path.GetFullPath(settings.File!))
            : entry.SchemaSource;
        var newEntry = new ServiceEntry(entry.Name, entry.BaseUrl, newSource, newEndpointCount, DateTimeOffset.UtcNow);
        await ServiceRegistry.UpsertAsync(newEntry, newBytes!);

        AnsiConsole.MarkupLine("  [green]✓[/] Registry updated");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]{settings.Name}[/] is up to date.");
        return 0;
    }

    private async Task<(byte[]? bytes, string? error)> LoadNewBytesAsync(Settings settings, ServiceEntry entry, CancellationToken cancellationToken)
    {
        if (settings.File is not null)
        {
            if (!System.IO.File.Exists(settings.File))
                return (null, $"  [red]✕[/] File not found: [grey]{settings.File}[/]");
            return (await System.IO.File.ReadAllBytesAsync(settings.File, cancellationToken), null);
        }

        // URL mode
        try
        {
            var bytes = await httpClientFactory.CreateClient().GetByteArrayAsync(entry.SchemaSource.Value, cancellationToken);
            return (bytes, null);
        }
        catch
        {
            return (null, $"  [red]✕[/] Could not reach schema URL: [grey]{entry.SchemaSource.Value}[/]\n" +
                          $"    [grey]→ Service may be unavailable. Try again or provide a local file with --file.[/]");
        }
    }

    private static List<(string Method, string Path, string OperationId)> ExtractOperations(OpenApiDocument? doc) =>
        doc?.Paths?
            .Where(p => p.Value.Operations is not null)
            .SelectMany(p => p.Value.Operations!.Select(op => (
                Method: op.Key.ToString().ToUpperInvariant(),
                Path: p.Key,
                OperationId: op.Value.OperationId ?? ""
            )))
            .ToList() ?? [];
}
