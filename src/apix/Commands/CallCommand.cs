using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using apix.Helpers;
using apix.Models;
using apix.Services;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class CallCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<CallCommand.Settings>
{

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<service>")]
        [Description("Name of the registered service")]
        public required string Service { get; init; }

        [CommandArgument(1, "<operationId>")]
        [Description("Operation ID to call (see 'apix endpoint list <service>')")]
        public required string OperationId { get; init; }

        [CommandOption("-v|--verbose")]
        [Description("Print full request/response headers and full response body")]
        public bool Verbose { get; init; }

        [CommandOption("--no-save")]
        [Description("Execute but do not save to history")]
        public bool NoSave { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve service
        var entry = await ServiceRegistry.FindAsync(settings.Service);
        if (entry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Service not found: [grey]{settings.Service}[/]");
            AnsiConsole.MarkupLine($"    [grey]→ Run [[apix service list]] to see registered services.[/]");
            return 1;
        }

        // 2. Parse schema
        await using var schemaStream = ServiceRegistry.OpenSchema(settings.Service);
        var reader = new OpenApiJsonReader();
        var result = await reader.ReadAsync(schemaStream, new OpenApiReaderSettings(), cancellationToken);
        if (result.Document is not { } doc)
        {
            AnsiConsole.MarkupLine("  [red]✕[/] Failed to read stored schema.");
            return 1;
        }

        // 3. Find operation by operationId
        var found = FindOperation(doc, settings.OperationId);
        if (found is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Operation not found: [grey]{settings.OperationId}[/]");
            var available = ListOperationIds(doc);
            if (available.Count > 0)
            {
                AnsiConsole.MarkupLine($"    [grey]→ Available operations in [white]{settings.Service}[/][grey]:[/]");
                foreach (var id in available)
                    AnsiConsole.MarkupLine($"      [grey]{id}[/]");
            }
            return 1;
        }

        var (pathPattern, method, operation) = found.Value;

        // 4. Build call inputs (open editor if needed)
        var pathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? requestBody = null;

        var template = EditorService.GenerateTemplate(operation);
        if (template is not null)
        {
            string editedJson;
            try
            {
                editedJson = await EditorService.OpenForEditAsync(template, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("  [yellow]⚠[/] Cancelled.");
                return 1;
            }

            if (!TryParseInputs(editedJson, out pathParams, out queryParams, out headerParams, out requestBody, out var parseError))
            {
                AnsiConsole.MarkupLine($"  [red]✕[/] Invalid JSON in editor: {parseError}");
                return 1;
            }
        }

        // 5. Build URL
        var url = BuildUrl(entry.BaseUrl, pathPattern, pathParams, queryParams);

        // 6. Build HTTP request
        using var request = new HttpRequestMessage(method, url);

        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (k, v) in headerParams)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                request.Headers.TryAddWithoutValidation(k, v);
                requestHeaders[k] = v;
            }
        }

        if (requestBody is not null)
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            requestHeaders["Content-Type"] = "application/json";
        }

        var sepWidth = Math.Max(40, $"► {method} {url}".Length);

        // 7. Print request block
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine("  [grey]REQUEST[/]");
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  {method} {url}");
        if (settings.Verbose)
        {
            OutputHelpers.PrintHeaders(requestHeaders);
            if (requestBody is not null)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("  [grey]Body:[/]");
                OutputHelpers.PrintBody(requestBody, full: true);
            }
        }
        AnsiConsole.WriteLine();

        // 8. Execute
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            var client = httpClientFactory.CreateClient();
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            AnsiConsole.MarkupLine($"[red]✕[/] Could not connect to [grey]{entry.BaseUrl}[/]");
            AnsiConsole.MarkupLine($"  [grey]→ Service may be unavailable or base URL is incorrect.[/]");
            return 1;
        }

        stopwatch.Stop();
        var durationMs = stopwatch.ElapsedMilliseconds;

        // 9. Read response
        var statusCode = (int)response.StatusCode;
        var statusText = response.ReasonPhrase ?? response.StatusCode.ToString();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        var isSuccess = statusCode is >= 200 and < 400;
        var statusColor = isSuccess ? "green" : "red";
        var statusIcon = isSuccess ? "◆" : "✕";

        // 10. Print response
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  [grey]RESPONSE[/]  [{statusColor}]{statusIcon} {statusCode} {statusText}[/]  [grey][[{durationMs}ms]][/]");
        OutputHelpers.Separator(sepWidth);
        if (settings.Verbose)
        {
            OutputHelpers.PrintHeaders(responseHeaders);
        }

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [grey]Body:[/]");
            var truncated = OutputHelpers.PrintBody(responseBody, settings.Verbose);
            if (truncated)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"  [grey]... lines truncated — rerun with --verbose to print in full[/]");
            }
        }

        // 11. Save to history
        int? savedId = null;
        if (!settings.NoSave)
        {
            var historyEntry = new HistoryEntry(
                Id: 0,
                Timestamp: DateTimeOffset.UtcNow,
                Method: method.Method,
                Url: url,
                OperationId: settings.OperationId,
                RequestHeaders: requestHeaders,
                RequestBody: requestBody,
                StatusCode: statusCode,
                StatusText: statusText,
                ResponseHeaders: responseHeaders,
                ResponseBody: responseBody,
                DurationMs: durationMs
            );
            savedId = await HistoryService.AppendAsync(settings.Service, historyEntry);
        }

        // 12. Footer
        AnsiConsole.WriteLine();
        if (savedId.HasValue)
        {
            AnsiConsole.MarkupLine(
                $"  [grey]Saved as [white]#{savedId}[/] — " +
                $"[[apix history {settings.Service} {savedId}]] to inspect · " +
                $"[[apix replay {settings.Service} {savedId}]] to replay[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("  [grey](not saved to history)[/]");
        }

        return 0;
    }

    private static (string PathPattern, HttpMethod Method, OpenApiOperation Op)? FindOperation(OpenApiDocument doc, string operationId)
    {
        if (doc.Paths is null) return null;

        foreach (var (path, pathItem) in doc.Paths)
        {
            if (pathItem.Operations is null) continue;
            foreach (var (method, op) in pathItem.Operations)
            {
                if (string.Equals(op.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                    return (path, method, op);
            }
        }
        return null;
    }

    private static List<string> ListOperationIds(OpenApiDocument doc) =>
        doc.Paths?
            .Where(p => p.Value.Operations is not null)
            .SelectMany(p => p.Value.Operations!.Select(op => op.Value.OperationId ?? ""))
            .Where(id => id.Length > 0)
            .OrderBy(id => id)
            .ToList() ?? [];

    private static bool TryParseInputs(
        string json,
        out Dictionary<string, string> pathParams,
        out Dictionary<string, string> queryParams,
        out Dictionary<string, string> headers,
        out string? body,
        out string? error)
    {
        pathParams = new(StringComparer.OrdinalIgnoreCase);
        queryParams = new(StringComparer.OrdinalIgnoreCase);
        headers = new(StringComparer.OrdinalIgnoreCase);
        body = null;
        error = null;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }

        if (root is not JsonObject rootObj)
        {
            error = "Root must be a JSON object.";
            return false;
        }

        if (rootObj["path"] is JsonObject pathObj)
            foreach (var (k, v) in pathObj)
                pathParams[k] = v?.ToString() ?? "";

        if (rootObj["query"] is JsonObject queryObj)
            foreach (var (k, v) in queryObj)
                queryParams[k] = v?.ToString() ?? "";

        if (rootObj["headers"] is JsonObject headersObj)
            foreach (var (k, v) in headersObj)
                headers[k] = v?.ToString() ?? "";

        if (rootObj["body"] is JsonNode bodyNode)
            body = bodyNode.ToJsonString();

        return true;
    }

    private static string BuildUrl(string baseUrl, string pathPattern, Dictionary<string, string> pathParams, Dictionary<string, string> queryParams)
    {
        var path = pathPattern;
        foreach (var (k, v) in pathParams)
            path = path.Replace($"{{{k}}}", Uri.EscapeDataString(v), StringComparison.OrdinalIgnoreCase);

        var url = baseUrl.TrimEnd('/') + path;

        if (queryParams.Count > 0)
        {
            var qs = HttpUtility.ParseQueryString(string.Empty);
            foreach (var (k, v) in queryParams)
                qs[k] = v;
            url += "?" + qs;
        }

        return url;
    }

}
