using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using apix.Helpers;
using apix.Models;
using apix.Services;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands;

public class ReplayCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<ReplayCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<service>")]
        [Description("Name of the registered service")]
        public required string Service { get; init; }

        [CommandArgument(1, "<id>")]
        [Description("History entry ID to replay")]
        public required int Id { get; init; }

        [CommandOption("--no-save")]
        [Description("Execute but do not save to history")]
        public bool NoSave { get; init; }

        [CommandOption("-v|--verbose")]
        [Description("Print full request/response headers and full response body")]
        public bool Verbose { get; init; }

        [CommandOption("-e|--edit")]
        [Description("Open editor pre-filled with stored request values before sending")]
        public bool Edit { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve service
        var serviceEntry = await ServiceRegistry.FindAsync(settings.Service);
        if (serviceEntry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Service not found: [grey]{settings.Service}[/]");
            var allNames = (await ServiceRegistry.LoadAllAsync()).Select(e => e.Name);
            var suggestion = StringHelpers.FindClosestMatch(settings.Service, allNames);
            AnsiConsole.MarkupLine(suggestion is not null
                ? $"    [grey]→ Did you mean: [white]{Markup.Escape(suggestion)}[/]?[/]"
                : $"    [grey]→ Run [[apix service list]] to see registered services.[/]");
            return 1;
        }

        // 2. Load history entry
        var historyEntry = await HistoryService.FindAsync(settings.Service, settings.Id);
        if (historyEntry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Entry [white]#{settings.Id}[/] not found in [grey]{settings.Service}[/] history");
            return 1;
        }

        // 3. Resolve URL, headers, body
        string url;
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? requestBody;

        if (settings.Edit)
        {
            // Try to load schema and find operation for pre-filling
            JsonObject? template = null;
            string? pathPattern = null;

            try
            {
                await using var schemaStream = ServiceRegistry.OpenSchema(settings.Service);
                var reader = new OpenApiJsonReader();
                var result = await reader.ReadAsync(schemaStream, new OpenApiReaderSettings(), cancellationToken);
                if (result.Document is { } doc && historyEntry.OperationId is not null)
                {
                    var found = FindOperation(doc, historyEntry.OperationId);
                    if (found is not null)
                    {
                        var (pp, _, operation) = found.Value;
                        pathPattern = pp;
                        template = EditorService.GenerateTemplate(operation);
                        if (template is not null)
                            PreFillTemplate(template, historyEntry, pp);
                    }
                }
            }
            catch
            {
                // Fall back to blank template
            }

            // If no template from schema, build a minimal one from stored values
            template ??= BuildFallbackTemplate(historyEntry);

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

            if (!RequestHelpers.TryParseInputs(editedJson, out var pathParams, out var queryParams, out var headerParams, out requestBody, out var parseError))
            {
                AnsiConsole.MarkupLine($"  [red]✕[/] Invalid JSON in editor: {parseError}");
                return 1;
            }

            var effectivePath = pathPattern ?? new Uri(historyEntry.Url).AbsolutePath;
            url = RequestHelpers.BuildUrl(serviceEntry.BaseUrl, effectivePath, pathParams, queryParams);

            foreach (var (k, v) in headerParams)
                if (!string.IsNullOrWhiteSpace(v))
                    requestHeaders[k] = v;
        }
        else
        {
            // Use stored values directly
            url = historyEntry.Url;
            requestBody = historyEntry.RequestBody;
            foreach (var (k, v) in historyEntry.RequestHeaders)
                if (!string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    requestHeaders[k] = v;
        }

        var method = new HttpMethod(historyEntry.Method);

        AnsiConsole.MarkupLine($"  [grey]Replaying [white]#{settings.Id}[/] — {historyEntry.Method} {Markup.Escape(url)}[/]");
        AnsiConsole.WriteLine();

        // 4. Build HTTP request
        using var request = new HttpRequestMessage(method, url);

        foreach (var (k, v) in requestHeaders)
            request.Headers.TryAddWithoutValidation(k, v);

        if (requestBody is not null)
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            requestHeaders["Content-Type"] = "application/json";
        }

        var sepWidth = Math.Max(40, $"  {historyEntry.Method} {url}".Length);

        // 5. Print request block
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine("  [grey]REQUEST[/]");
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  {historyEntry.Method} {Markup.Escape(url)}");
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

        // 6. Execute
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        HttpRequestException? connectError = null;

        await AnsiConsole.Status()
            .StartAsync("Sending request…", async ctx =>
            {
                try
                {
                    var client = httpClientFactory.CreateClient();
                    response = await client.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    connectError = ex;
                }
            });

        stopwatch.Stop();

        if (connectError is not null)
        {
            AnsiConsole.MarkupLine($"[red]✕[/] Could not connect to [grey]{serviceEntry.BaseUrl}[/]");
            AnsiConsole.MarkupLine($"  [grey]→ Service may be unavailable or base URL is incorrect.[/]");
            return 1;
        }

        var durationMs = stopwatch.ElapsedMilliseconds;

        // 7. Read response
        var statusCode = (int)response!.StatusCode;
        var statusText = response.ReasonPhrase ?? response.StatusCode.ToString();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        var isSuccess = statusCode is >= 200 and < 400;
        var statusColor = isSuccess ? "green" : "red";
        var statusIcon = isSuccess ? "◆" : "✕";

        // 8. Print response
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  [grey]RESPONSE[/]  [{statusColor}]{statusIcon} {statusCode} {statusText}[/]  [grey][[{durationMs}ms]][/]");
        OutputHelpers.Separator(sepWidth);
        if (settings.Verbose)
            OutputHelpers.PrintHeaders(responseHeaders);

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

        // 9. Save to history
        int? savedId = null;
        if (!settings.NoSave)
        {
            var newEntry = new HistoryEntry(
                Id: 0,
                Timestamp: DateTimeOffset.UtcNow,
                Method: historyEntry.Method,
                Url: url,
                OperationId: historyEntry.OperationId,
                RequestHeaders: requestHeaders,
                RequestBody: requestBody,
                StatusCode: statusCode,
                StatusText: statusText,
                ResponseHeaders: responseHeaders,
                ResponseBody: responseBody,
                DurationMs: durationMs
            );
            savedId = await HistoryService.AppendAsync(settings.Service, newEntry);
        }

        // 10. Footer
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

    private static void PreFillTemplate(JsonObject template, HistoryEntry entry, string pathPattern)
    {
        // Pre-fill path params by matching pattern against stored URL path
        if (template["path"] is JsonObject pathObj)
        {
            try
            {
                var storedPath = new Uri(entry.Url).AbsolutePath;
                var regexPattern = Regex.Replace(
                    Regex.Escape(pathPattern),
                    @"\\{(\w+)\\}",
                    m => $"(?<{m.Groups[1].Value}>[^/?]+)");

                var match = Regex.Match(storedPath, $"^{regexPattern}$");
                if (match.Success)
                {
                    foreach (var key in pathObj.Select(p => p.Key).ToList())
                    {
                        var group = match.Groups[key];
                        if (group.Success)
                            pathObj[key] = JsonValue.Create(Uri.UnescapeDataString(group.Value));
                    }
                }
            }
            catch { /* best-effort */ }
        }

        // Pre-fill query params from stored URL
        if (template["query"] is JsonObject queryObj)
        {
            try
            {
                var storedUri = new Uri(entry.Url);
                var qs = HttpUtility.ParseQueryString(storedUri.Query);
                foreach (var key in queryObj.Select(p => p.Key).ToList())
                {
                    var value = qs[key];
                    if (value is not null)
                        queryObj[key] = JsonValue.Create(value);
                }
            }
            catch { /* best-effort */ }
        }

        // Pre-fill headers (excluding Content-Type)
        if (template["headers"] is JsonObject headersObj)
        {
            foreach (var (k, v) in entry.RequestHeaders)
            {
                if (string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue;
                headersObj[k] = JsonValue.Create(v);
            }
        }

        // Pre-fill body
        if (entry.RequestBody is not null && template.ContainsKey("body"))
        {
            try
            {
                var bodyNode = JsonNode.Parse(entry.RequestBody);
                if (bodyNode is not null)
                    template["body"] = bodyNode.DeepClone();
            }
            catch { /* best-effort */ }
        }
    }

    private static JsonObject BuildFallbackTemplate(HistoryEntry entry)
    {
        var root = new JsonObject();

        try
        {
            var storedUri = new Uri(entry.Url);
            var qs = HttpUtility.ParseQueryString(storedUri.Query);
            if (qs.Count > 0)
            {
                var queryObj = new JsonObject();
                foreach (string key in qs.Keys)
                    if (key is not null)
                        queryObj[key] = JsonValue.Create(qs[key] ?? "");
                root["query"] = queryObj;
            }
        }
        catch { /* best-effort */ }

        var filteredHeaders = entry.RequestHeaders
            .Where(h => !string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (filteredHeaders.Count > 0)
        {
            var headersObj = new JsonObject();
            foreach (var (k, v) in filteredHeaders)
                headersObj[k] = JsonValue.Create(v);
            root["headers"] = headersObj;
        }

        if (entry.RequestBody is not null)
        {
            try
            {
                var bodyNode = JsonNode.Parse(entry.RequestBody);
                if (bodyNode is not null)
                    root["body"] = bodyNode.DeepClone();
            }
            catch
            {
                root["body"] = JsonValue.Create(entry.RequestBody);
            }
        }

        return root;
    }
}
