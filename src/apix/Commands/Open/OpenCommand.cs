using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using apix.Helpers;
using apix.Models;
using apix.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace apix.Commands.Open;

public class OpenCommand(IHttpClientFactory httpClientFactory) : AsyncCommand<OpenCommand.Settings>
{
    internal const string HistoryKey = "_open";

    public class Settings : CommandSettings
    {
        [CommandOption("-u|--url")]
        [Description("Full URL to send the request to (include query params in the URL directly)")]
        public string? Url { get; init; }

        [CommandOption("-x|--method")]
        [Description("HTTP method: GET, POST, PUT, DELETE, PATCH, HEAD. Default: GET")]
        public string Method { get; init; } = "GET";

        [CommandOption("-v|--verbose")]
        [Description("Print full request/response headers and full response body")]
        public bool Verbose { get; init; }

        [CommandOption("--no-save")]
        [Description("Execute but do not save to history")]
        public bool NoSave { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // 1. Validate URL
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            AnsiConsole.MarkupLine("  [red]✕[/] URL is required.");
            AnsiConsole.MarkupLine("    [grey]→ Usage: apix open --url <url> [-x METHOD][/]");
            return 1;
        }

        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Invalid URL: [grey]{Markup.Escape(settings.Url)}[/]");
            AnsiConsole.MarkupLine("    [grey]→ Provide a full URL including the scheme, e.g. https://example.com/path[/]");
            return 1;
        }

        var method = new HttpMethod(settings.Method.ToUpperInvariant());
        var hasBody = method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;

        // 2. Build editor template
        var template = new JsonObject
        {
            ["headers"] = new JsonObject()
        };
        if (hasBody)
            template["body"] = new JsonObject();

        // 3. Open editor
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

        // 4. Parse editor output — only headers and body matter here
        if (!RequestHelpers.TryParseInputs(editedJson, out _, out _, out var headerParams, out var requestBody, out var parseError))
        {
            AnsiConsole.MarkupLine($"  [red]✕[/] Invalid JSON in editor: {parseError}");
            return 1;
        }

        var url = settings.Url;

        // 5. Build HTTP request
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

        var sepWidth = Math.Max(40, $"  {method} {url}".Length);

        // 6. Print request block
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

        // 7. Execute
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

        // 8. Read response
        var statusCode = (int)response!.StatusCode;
        var statusText = response.ReasonPhrase ?? response.StatusCode.ToString();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        var isSuccess = statusCode is >= 200 and < 400;
        var statusColor = isSuccess ? "green" : "red";
        var statusIcon = isSuccess ? "◆" : "✕";

        // 9. Print response
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

        // 10. Save to history
        int? savedId = null;
        if (!settings.NoSave)
        {
            var historyEntry = new HistoryEntry(
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
            savedId = await HistoryService.AppendAsync(HistoryKey, historyEntry);
        }

        // 11. Footer
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
}
