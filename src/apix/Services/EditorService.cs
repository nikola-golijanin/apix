using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;

namespace apix.Services;

public static class EditorService
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task<string> ResolveEditorAsync()
    {
        var configured = await ConfigService.GetEditorAsync();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var envEditor = Environment.GetEnvironmentVariable("EDITOR");
        if (!string.IsNullOrWhiteSpace(envEditor))
            return envEditor;

        return OperatingSystem.IsWindows() ? "notepad" : "nano";
    }

    /// <summary>
    /// Builds a JSON template object for the given operation.
    /// Returns null if the operation has no path params, query params, or JSON body.
    /// </summary>
    public static JsonObject? GenerateTemplate(OpenApiOperation op)
    {
        var pathParams = op.Parameters?
            .Where(p => p.In == ParameterLocation.Path)
            .ToList() ?? [];

        var queryParams = op.Parameters?
            .Where(p => p.In == ParameterLocation.Query)
            .ToList() ?? [];

        var headerParams = op.Parameters?
            .Where(p => p.In == ParameterLocation.Header)
            .ToList() ?? [];

        IOpenApiSchema? bodySchema = null;
        if (op.RequestBody?.Content is { } content &&
            content.TryGetValue("application/json", out var mediaType))
        {
            bodySchema = mediaType.Schema;
        }

        if (pathParams.Count == 0 && queryParams.Count == 0 && headerParams.Count == 0 && bodySchema is null)
            return null;

        var root = new JsonObject();

        if (pathParams.Count > 0)
        {
            var pathObj = new JsonObject();
            foreach (var p in pathParams)
                pathObj[p.Name!] = JsonValue.Create(SchemaToHint(p.Schema, required: true));
            root["path"] = pathObj;
        }

        if (queryParams.Count > 0)
        {
            var queryObj = new JsonObject();
            foreach (var p in queryParams)
                queryObj[p.Name!] = JsonValue.Create(SchemaToHint(p.Schema, required: p.Required));
            root["query"] = queryObj;
        }

        if (headerParams.Count > 0)
        {
            var headersObj = new JsonObject();
            foreach (var p in headerParams)
                headersObj[p.Name!] = JsonValue.Create(SchemaToHint(p.Schema, required: p.Required));
            root["headers"] = headersObj;
        }

        if (bodySchema is not null)
            root["body"] = SchemaToNode(bodySchema, depth: 0);

        return root;
    }

    /// <summary>
    /// Writes the template to a temp file, opens the editor, waits for exit, and returns the edited content.
    /// </summary>
    public static async Task<string> OpenForEditAsync(JsonObject template, CancellationToken cancellationToken = default)
    {
        var tempFile = Path.GetTempFileName();
        // Rename to .json so editors apply syntax highlighting
        var jsonFile = tempFile + ".json";
        File.Move(tempFile, jsonFile);

        try
        {
            var content = template.ToJsonString(PrettyJson).ReplaceLineEndings(Environment.NewLine);
            await File.WriteAllTextAsync(jsonFile, content, cancellationToken);

            var editor = await ResolveEditorAsync();
            await LaunchEditorAsync(editor, jsonFile, cancellationToken);

            return await File.ReadAllTextAsync(jsonFile, cancellationToken);
        }
        finally
        {
            if (File.Exists(jsonFile))
                File.Delete(jsonFile);
        }
    }

    private static async Task LaunchEditorAsync(string editor, string filePath, CancellationToken cancellationToken)
    {
        // Split "code --wait" into executable + args prefix
        var parts = editor.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var executable = parts[0];
        var argsPrefix = parts.Length > 1 ? parts[1] : "";

        // VS Code needs --wait to block; add it if missing
        if (executable.Contains("code", StringComparison.OrdinalIgnoreCase) &&
            !argsPrefix.Contains("--wait", StringComparison.OrdinalIgnoreCase))
        {
            argsPrefix = string.IsNullOrEmpty(argsPrefix) ? "--wait" : argsPrefix + " --wait";
        }

        var arguments = string.IsNullOrEmpty(argsPrefix)
            ? $"\"{filePath}\""
            : $"{argsPrefix} \"{filePath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start editor: {executable}");

        await process.WaitForExitAsync(cancellationToken);
    }

    private static JsonNode SchemaToNode(IOpenApiSchema? schema, int depth)
    {
        if (schema is null)
            return JsonValue.Create("unknown")!;

        // Enum: use first value as default
        if (schema.Enum is { Count: > 0 } enumValues)
        {
            var first = enumValues[0]?.ToString() ?? "<string>";
            return JsonValue.Create(first)!;
        }

        // Object with properties
        if (schema.Properties is { Count: > 0 } props)
        {
            var obj = new JsonObject();
            foreach (var (name, propSchema) in props)
            {
                var required = schema.Required?.Contains(name) ?? false;
                obj[name] = depth < 2
                    ? SchemaToNode(propSchema, depth + 1)
                    : JsonValue.Create(SchemaToHint(propSchema, required))!;
            }
            return obj;
        }

        // Array
        var type = schema.Type;
        if (HasType(type, JsonSchemaType.Array) || schema.Items is not null)
        {
            var itemHint = schema.Items is not null
                ? SchemaToNode(schema.Items, depth + 1)
                : JsonValue.Create("unknown")!;
            return new JsonArray(itemHint);
        }

        return JsonValue.Create(SchemaToHint(schema, required: true))!;
    }

    private static string SchemaToHint(IOpenApiSchema? schema, bool required)
    {
        if (schema is null)
            return required ? "unknown" : "unknown?";

        // Enum: list options
        if (schema.Enum is { Count: > 0 } enumValues)
        {
            var options = string.Join(" | ", enumValues.Select(e => e?.ToString() ?? ""));
            return required ? options : $"{options}?";
        }

        var type = schema.Type;
        var hint = true switch
        {
            true when HasType(type, JsonSchemaType.Integer) => "integer",
            true when HasType(type, JsonSchemaType.Number)  => "number",
            true when HasType(type, JsonSchemaType.Boolean) => "boolean",
            true when HasType(type, JsonSchemaType.Array)   => "array",
            true when HasType(type, JsonSchemaType.Object)  => "object",
            true when HasType(type, JsonSchemaType.String)  => "string",
            _                                               => "string"
        };

        return required ? hint : $"{hint}?";
    }

    private static bool HasType(JsonSchemaType? type, JsonSchemaType flag) =>
        type.HasValue && (type.Value & flag) != 0;
}
