using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using apix.Helpers;
using apix.Models;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Open;

public class OpenReplayCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<OpenReplayCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("History entry ID to replay")]
        public required int Id { get; init; }

        [CommandOption("-e|--edit")]
        [Description("Open editor pre-filled with stored URL, method, headers, and body before sending")]
        public bool Edit { get; init; }

        [CommandOption("--no-save")]
        [Description("Execute but do not save to history")]
        public bool NoSave { get; init; }

        [CommandOption("-v|--verbose")]
        [Description("Print full request/response headers and full response body")]
        public bool Verbose { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // 1. Load history entry
        var historyEntry = await HistoryService.FindAsync(OpenCommand.HistoryKey, settings.Id);
        if (historyEntry is null)
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] No open request found with id [white]#{settings.Id}[/].");
            AnsiConsole.MarkupLine("    [grey]→ Run [[apix open history]] to see available requests.[/]");
            return 1;
        }

        // 2. Resolve URL, method, headers, body
        string url;
        HttpMethod method;
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? requestBody;

        if (settings.Edit)
        {
            var template = BuildEditTemplate(historyEntry);

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

            if (!TryParseEditTemplate(editedJson, out var parsedUrl, out var parsedMethod, out var parsedHeaders, out requestBody, out var parseError))
            {
                AnsiConsole.MarkupLine($"  [red]✕[/] Invalid JSON in editor: {parseError}");
                return 1;
            }

            url    = parsedUrl    ?? historyEntry.Url;
            method = new HttpMethod((parsedMethod ?? historyEntry.Method).ToUpperInvariant());

            foreach (var (k, v) in parsedHeaders)
                if (!string.IsNullOrWhiteSpace(v))
                    requestHeaders[k] = v;
        }
        else
        {
            url    = historyEntry.Url;
            method = new HttpMethod(historyEntry.Method);
            requestBody = historyEntry.RequestBody;

            foreach (var (k, v) in historyEntry.RequestHeaders)
                if (!string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    requestHeaders[k] = v;
        }

        AnsiConsole.MarkupLine($"  [grey]Replaying [white]#{settings.Id}[/] — {method} {Markup.Escape(url)}[/]");
        AnsiConsole.WriteLine();

        // 3. Build HTTP request
        using var request = new HttpRequestMessage(method, url);

        foreach (var (k, v) in requestHeaders)
            request.Headers.TryAddWithoutValidation(k, v);

        if (requestBody is not null)
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            requestHeaders["Content-Type"] = "application/json";
        }

        var sepWidth = Math.Max(40, $"  {method} {url}".Length);

        // 4. Print request block
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine("  [grey]REQUEST[/]");
        OutputHelpers.Separator(sepWidth);
        AnsiConsole.MarkupLine($"  {method} {Markup.Escape(url)}");
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

        // 5. Execute
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
            AnsiConsole.MarkupLine($"[red]✕[/] Could not connect to [grey]{Markup.Escape(url)}[/]");
            AnsiConsole.MarkupLine("  [grey]→ Check the URL and your network connection.[/]");
            return 1;
        }

        var durationMs = stopwatch.ElapsedMilliseconds;

        // 6. Read response
        var statusCode = (int)response!.StatusCode;
        var statusText = response.ReasonPhrase ?? response.StatusCode.ToString();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        var isSuccess = statusCode is >= 200 and < 400;
        var statusColor = isSuccess ? "green" : "red";
        var statusIcon  = isSuccess ? "◆" : "✕";

        // 7. Print response
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
                AnsiConsole.MarkupLine("  [grey]... lines truncated — rerun with --verbose to print in full[/]");
            }
        }

        // 8. Save to history
        int? savedId = null;
        if (!settings.NoSave)
        {
            var newEntry = new HistoryEntry(
                Id: 0,
                Timestamp: DateTimeOffset.UtcNow,
                Method: method.Method,
                Url: url,
                OperationId: null,
                RequestHeaders: requestHeaders,
                RequestBody: requestBody,
                StatusCode: statusCode,
                StatusText: statusText,
                ResponseHeaders: responseHeaders,
                ResponseBody: responseBody,
                DurationMs: durationMs
            );
            savedId = await HistoryService.AppendAsync(OpenCommand.HistoryKey, newEntry);
        }

        // 9. Footer
        AnsiConsole.WriteLine();
        if (savedId.HasValue)
        {
            AnsiConsole.MarkupLine(
                $"  [grey]Saved as [white]#{savedId}[/] — " +
                $"[[apix open history {savedId}]] to inspect · " +
                $"[[apix open replay {savedId}]] to replay[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("  [grey](not saved to history)[/]");
        }

        return 0;
    }

    private static JsonObject BuildEditTemplate(HistoryEntry entry)
    {
        var root = new JsonObject
        {
            ["url"]    = JsonValue.Create(entry.Url),
            ["method"] = JsonValue.Create(entry.Method)
        };

        var headersObj = new JsonObject();
        foreach (var (k, v) in entry.RequestHeaders)
            if (!string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                headersObj[k] = JsonValue.Create(v);
        root["headers"] = headersObj;

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

    private static bool TryParseEditTemplate(
        string json,
        out string? url,
        out string? method,
        out Dictionary<string, string> headers,
        out string? body,
        out string? error)
    {
        url = null;
        method = null;
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

        url    = rootObj["url"]?.GetValue<string>();
        method = rootObj["method"]?.GetValue<string>();

        if (rootObj["headers"] is JsonObject headersObj)
            foreach (var (k, v) in headersObj)
                headers[k] = v?.ToString() ?? "";

        if (rootObj["body"] is JsonNode bodyNode)
            body = bodyNode.ToJsonString();

        return true;
    }
}
