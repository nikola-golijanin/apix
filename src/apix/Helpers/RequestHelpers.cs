using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace apix.Helpers;

public static class RequestHelpers
{
    public static bool TryParseInputs(
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

    public static string BuildUrl(string baseUrl, string pathPattern, Dictionary<string, string> pathParams, Dictionary<string, string> queryParams)
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
