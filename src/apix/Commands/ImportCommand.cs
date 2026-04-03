using System.ComponentModel;
using apix.Models;
using apix.Services;
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
        [CommandOption("-n|--name <name>")]
        [Description("Name to register the service under")]
        public required string Name { get; init; }

        [CommandOption("--base-url <url>")]
        [Description("Base URL of the service")]
        public string? BaseUrl { get; init; }

        [CommandOption("-s|--schema <path|url>")]
        [Description("Local file path or URL to the OpenAPI schema")]
        public required string Schema { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
                return ValidationResult.Error("--base-url is required.");
            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        byte[]? schemaBytes = null;
        var isUrl = false;
        string? loadError = null;
        OpenApiDocument? document = null;
        string? versionLabel = null;
        var endpointCount = 0;
        var parseFailed = false;

        await AnsiConsole.Status()
            .StartAsync("Fetching schema…", async ctx =>
            {
                var (bytes, url, error) = await LoadSchemaBytesAsync(settings.Schema, cancellationToken);
                if (error is not null) { loadError = error; return; }

                schemaBytes = bytes;
                isUrl = url;

                ctx.Status("Parsing schema…");
                var reader = new OpenApiJsonReader();
                var result = await reader.ReadAsync(new MemoryStream(schemaBytes!), new OpenApiReaderSettings(), cancellationToken);

                if (result.Diagnostic?.Errors.Count > 0 || result.Document is not { } doc)
                {
                    parseFailed = true;
                    return;
                }

                document = doc;
                versionLabel = result.Diagnostic?.SpecificationVersion switch
                {
                    OpenApiSpecVersion.OpenApi2_0 => "OpenAPI 2.0",
                    OpenApiSpecVersion.OpenApi3_0 => "OpenAPI 3.0",
                    OpenApiSpecVersion.OpenApi3_1 => "OpenAPI 3.1",
                    _ => "OpenAPI"
                };
                endpointCount = document.Paths?.Sum(p => p.Value.Operations?.Count ?? 0) ?? 0;
            });

        if (loadError is not null)
        {
            AnsiConsole.MarkupLine(loadError);
            return 1;
        }

        if (parseFailed)
        {
            AnsiConsole.MarkupLine("  [red]✕[/] Invalid OpenAPI schema — failed to parse document");
            AnsiConsole.MarkupLine("    [grey]→ Ensure the file is a valid OpenAPI 3.0 or 2.0 specification[/]");
            return 1;
        }

        var schemaSource = isUrl
            ? new SchemaSource(SchemaSourceType.Url, settings.Schema)
            : new SchemaSource(SchemaSourceType.File, Path.GetFullPath(settings.Schema));

        var entry = new ServiceEntry(settings.Name, settings.BaseUrl, schemaSource, endpointCount, DateTimeOffset.UtcNow);
        await ServiceRegistry.UpsertAsync(entry, schemaBytes!);

        AnsiConsole.MarkupLine($"  [green]✓[/] Schema loaded     [grey]({versionLabel})[/]");
        AnsiConsole.MarkupLine($"  [green]✓[/] {endpointCount} endpoints parsed");
        AnsiConsole.MarkupLine($"  [green]✓[/] Saved to registry");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]{settings.Name}[/] is ready. Run [grey][[apix endpoint list {settings.Name}]][/] to explore.");

        return 0;
    }

    private async Task<(byte[]? bytes, bool isUrl, string? error)> LoadSchemaBytesAsync(string schema, CancellationToken cancellationToken)
    {
        var isUrl = Uri.TryCreate(schema, UriKind.Absolute, out var uri)
                    && (uri.Scheme == "http" || uri.Scheme == "https");

        if (isUrl)
        {
            try
            {
                var bytes = await httpClientFactory.CreateClient().GetByteArrayAsync(schema, cancellationToken);
                return (bytes, true, null);
            }
            catch
            {
                var error = $"  [red]✕[/] Could not reach schema URL: [grey]{schema}[/]\n" +
                            $"    [grey]→ Check the URL or use a local file path with --schema ./openapi.json[/]";
                return (null, true, error);
            }
        }

        if (!File.Exists(schema))
            return (null, false, $"  [red]✕[/] File not found: [grey]{schema}[/]");

        return (await File.ReadAllBytesAsync(schema, cancellationToken), false, null);
    }
}
