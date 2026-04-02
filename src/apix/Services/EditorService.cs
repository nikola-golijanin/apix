using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;
using Microsoft.OpenApi.Models.References;

namespace apix.Services;

public static class EditorService
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly string[] KnownPresets = ["vim", "nano", "vscode", "notepad"];

    public static string ExpandPreset(string value) => value.ToLowerInvariant() switch
    {
        "vscode" => "code --wait",
        _        => value
    };

    public static async Task<string> ResolveEditorAsync()
    {
        var configured = await ConfigService.GetEditorAsync();
        if (!string.IsNullOrWhiteSpace(configured))
            return ExpandPreset(configured);

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

        var editorArgs = string.IsNullOrEmpty(argsPrefix)
            ? $"\"{filePath}\""
            : $"{argsPrefix} \"{filePath}\"";

        // On Windows, wrap in cmd.exe so batch files (code.cmd) are resolved correctly.
        ProcessStartInfo psi;
        if (OperatingSystem.IsWindows())
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {executable} {editorArgs}",
                UseShellExecute = false
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = editorArgs,
                UseShellExecute = false
            };
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start editor: {executable}");

        await process.WaitForExitAsync(cancellationToken);
    }

    /// Unwraps a reference to its target; returns the schema as-is otherwise.
    private static IOpenApiSchema? Resolve(IOpenApiSchema? schema) => schema switch
    {
        OpenApiSchemaReference r => r.Target ?? schema,
        _                        => schema
    };

    private static JsonNode SchemaToNode(IOpenApiSchema? raw, int depth)
    {
        var schema = Resolve(raw) ?? raw;
        if (schema is null)
            return JsonValue.Create("string")!;

        // Enum: use first value as hint
        if (schema.Enum is { Count: > 0 } enumValues)
        {
            var first = enumValues[0]?.ToString() ?? "string";
            return JsonValue.Create(first)!;
        }

        // Collect properties from direct definition AND allOf/anyOf/oneOf
        var props = CollectProperties(schema);

        if (props.Count > 0)
        {
            if (depth > 5) return JsonValue.Create("object")!;
            var obj = new JsonObject();
            foreach (var (name, propSchema) in props)
            {
                var required = schema.Required?.Contains(name) ?? false;
                obj[name] = SchemaToNode(propSchema, depth + 1);
            }
            return obj;
        }

        // Array
        if (HasType(schema.Type, JsonSchemaType.Array) || schema.Items is not null)
        {
            var itemNode = schema.Items is not null
                ? SchemaToNode(schema.Items, depth + 1)
                : JsonValue.Create("unknown")!;
            return new JsonArray(itemNode);
        }

        return JsonValue.Create(SchemaToHint(raw, required: true))!;
    }

    /// Merges properties from direct definition and allOf/anyOf/oneOf sub-schemas.
    private static Dictionary<string, IOpenApiSchema> CollectProperties(IOpenApiSchema schema)
    {
        var result = new Dictionary<string, IOpenApiSchema>(StringComparer.OrdinalIgnoreCase);

        if (schema.Properties is not null)
            foreach (var (k, v) in schema.Properties)
                result[k] = v;

        var combined = (schema.AllOf ?? []).Concat(schema.AnyOf ?? []).Concat(schema.OneOf ?? []);
        foreach (var sub in combined)
        {
            var resolved = Resolve(sub);
            if (resolved?.Properties is null) continue;
            foreach (var (k, v) in resolved.Properties)
                result[k] = v;
        }

        return result;
    }

    public static string SchemaToHint(IOpenApiSchema? raw, bool required)
    {
        var schema = Resolve(raw) ?? raw;
        if (schema is null)
            return required ? "string" : "string?";

        // Enum: list options
        if (schema.Enum is { Count: > 0 } enumValues)
        {
            var options = string.Join(" | ", enumValues.Select(e => e?.ToString() ?? ""));
            return required ? options : $"{options}?";
        }

        // Object: has direct properties or allOf/anyOf/oneOf
        var props = CollectProperties(schema);
        if (props.Count > 0)
            return required ? "object" : "object?";

        var hint = true switch
        {
            true when HasType(schema.Type, JsonSchemaType.Integer) => "integer",
            true when HasType(schema.Type, JsonSchemaType.Number)  => "number",
            true when HasType(schema.Type, JsonSchemaType.Boolean) => "boolean",
            true when HasType(schema.Type, JsonSchemaType.Array)   => "array",
            true when HasType(schema.Type, JsonSchemaType.Object)  => "object",
            true when HasType(schema.Type, JsonSchemaType.String)  => "string",
            _                                                      => "string"
        };

        return required ? hint : $"{hint}?";
    }

    private static bool HasType(JsonSchemaType? type, JsonSchemaType flag) =>
        type.HasValue && (type.Value & flag) != 0;
}
