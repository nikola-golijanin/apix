using System.ComponentModel;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class ImportCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<ImportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--name <name>")]
        [Description("Name to register the service under")]
        public required string Name { get; init; }

        [CommandOption("--base-url <url>")]
        [Description("Base URL of the service")]
        public required string BaseUrl { get; init; }

        [CommandOption("--schema <path|url>")]
        [Description("Local file path or URL to the OpenAPI schema")]
        public required string Schema { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Importing [cyan]{settings.Name}[/]...");
        AnsiConsole.WriteLine();

        var (stream, loadError) = await LoadSchemaAsync(settings.Schema, cancellationToken);
        if (loadError is not null)
        {
            AnsiConsole.MarkupLine(loadError);
            return 1;
        }

        var reader = new OpenApiJsonReader();
        var result = await reader.ReadAsync(stream!, new OpenApiReaderSettings(), cancellationToken);
        
        await stream!.DisposeAsync();

        if (result.Diagnostic?.Errors.Count > 0 || result.Document is not { } document)
        {
            AnsiConsole.MarkupLine("  [red]✕[/] Invalid OpenAPI schema — failed to parse document");
            AnsiConsole.MarkupLine("    [grey]→ Ensure the file is a valid OpenAPI 3.0 or 2.0 specification[/]");
            return 1;
        }

        var versionLabel = result.Diagnostic?.SpecificationVersion switch
        {
            OpenApiSpecVersion.OpenApi2_0 => "OpenAPI 2.0",
            OpenApiSpecVersion.OpenApi3_0 => "OpenAPI 3.0",
            OpenApiSpecVersion.OpenApi3_1 => "OpenAPI 3.1",
            _ => "OpenAPI"
        };

        var endpointCount = document.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;

        AnsiConsole.MarkupLine($"  [green]✓[/] Schema loaded     [grey]({versionLabel})[/]");
        AnsiConsole.MarkupLine($"  [green]✓[/] {endpointCount} endpoints parsed");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]{settings.Name}[/] is ready. Run [grey][[apix endpoints list {settings.Name}]][/] to explore.");

        return 0;
    }

    private async Task<(Stream? stream, string? error)> LoadSchemaAsync(string schema, CancellationToken cancellationToken)
    {
        var isUrl = Uri.TryCreate(schema, UriKind.Absolute, out var uri)
                    && (uri.Scheme == "http" || uri.Scheme == "https");

        if (isUrl)
        {
            try
            {
                var httpClient = httpClientFactory.CreateClient();
                var stream = await httpClient.GetStreamAsync(schema, cancellationToken);
                return (stream, null);
            }
            catch
            {
                var error = $"  [red]✕[/] Could not reach schema URL: [grey]{schema}[/]\n" +
                            $"    [grey]→ Check the URL or use a local file path with --schema ./openapi.json[/]";
                return (null, error);
            }
        }

        if (!File.Exists(schema))
            return (null, $"  [red]✕[/] File not found: [grey]{schema}[/]");

        return (File.OpenRead(schema), null);
    }
}
