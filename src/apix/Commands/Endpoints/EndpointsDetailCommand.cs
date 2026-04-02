using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using apix.Helpers;
using apix.Services;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;
using Microsoft.OpenApi.Models.References;
using Microsoft.OpenApi.Reader;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Endpoints;

public class EndpointsDetailCommand : AsyncCommand<EndpointsDetailCommand.Settings>
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<service>")]
        [Description("Name of the registered service")]
        public required string Service { get; init; }

        [CommandArgument(1, "<operationId>")]
        [Description("Operation ID to inspect")]
        public required string OperationId { get; init; }
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
        if (result.Document is not { } doc)
        {
            AnsiConsole.MarkupLine("  [red]✕[/] Failed to read stored schema.");
            return 1;
        }

        var found = FindOperation(doc, settings.OperationId);
        if (found is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Operation not found: [grey]{settings.OperationId}[/]");
            return 1;
        }

        var (pathPattern, method, op) = found.Value;

        var pathParams   = op.Parameters?.Where(p => p.In == ParameterLocation.Path).ToList()   ?? (List<IOpenApiParameter>)[];
        var queryParams  = op.Parameters?.Where(p => p.In == ParameterLocation.Query).ToList()  ?? (List<IOpenApiParameter>)[];
        var headerParams = op.Parameters?.Where(p => p.In == ParameterLocation.Header).ToList() ?? (List<IOpenApiParameter>)[];

        // Build URL line: plain path with {name} + ?q=...&q2=... for query
        var urlPath = pathPattern;
        var sepWidth = Math.Max(40, $"  {method.Method}  {urlPath}".Length + 20);

        if (queryParams.Count > 0)
        {
            var qs = string.Join("&", queryParams.Select(p => $"{p.Name}=..."));
            urlPath += "?" + qs;
        }

        // Header
        AnsiConsole.MarkupLine($"[cyan]{settings.Service}[/] [grey]— {settings.OperationId}[/]");
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  [white]{method.Method}[/]  [grey]{Markup.Escape(urlPath)}[/]");

        // Path Parameters
        if (pathParams.Count > 0)
            PrintParamSection("Path Parameters", pathParams, sepWidth);

        // Query Parameters
        if (queryParams.Count > 0)
            PrintParamSection("Query Parameters", queryParams, sepWidth);

        // Headers
        if (headerParams.Count > 0)
            PrintParamSection("Headers", headerParams, sepWidth);

        // Request Body
        var template = EditorService.GenerateTemplate(op);
        if (template?["body"] is JsonNode bodyNode)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [grey]Request Body  (application/json)[/]");
            OutputHelpers.Separator(sepWidth);
            var pretty = bodyNode.ToJsonString(PrettyJson);
            foreach (var line in pretty.Split('\n'))
                AnsiConsole.WriteLine("  " + line);
        }

        // Responses
        if (op.Responses is { Count: > 0 })
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [grey]Responses[/]");
            OutputHelpers.Separator(sepWidth);
            foreach (var (code, resp) in op.Responses.OrderBy(r => r.Key))
            {
                var desc = Markup.Escape(resp.Description ?? "");
                AnsiConsole.MarkupLine($"  [grey]{code}[/]  {desc}");
            }
        }

        return 0;
    }

    private static void PrintParamSection(string title, List<IOpenApiParameter> parameters, int sepWidth)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [grey]{title}[/]");
        OutputHelpers.Separator(sepWidth);

        var nameWidth = Math.Max(parameters.Max(p => p.Name?.Length ?? 0), 4);
        var typeWidth = Math.Max(parameters.Max(p => EditorService.SchemaToHint(p.Schema, p.Required).Length), 4);

        foreach (var p in parameters)
        {
            var name     = Markup.Escape((p.Name ?? "").PadRight(nameWidth));
            var type     = Markup.Escape(EditorService.SchemaToHint(p.Schema, p.Required).PadRight(typeWidth));
            var required = p.Required ? "[grey]✔[/]" : "  ";
            AnsiConsole.MarkupLine($"  [white]{name}[/]  [grey]{type}[/]  {required}");
        }
    }

    private static (string PathPattern, HttpMethod Method, OpenApiOperation Op)? FindOperation(OpenApiDocument doc, string operationId)
    {
        if (doc.Paths is null) return null;
        foreach (var (path, pathItem) in doc.Paths)
        {
            if (pathItem.Operations is null) continue;
            foreach (var (method, op) in pathItem.Operations)
                if (string.Equals(op.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                    return (path, method, op);
        }
        return null;
    }
}
