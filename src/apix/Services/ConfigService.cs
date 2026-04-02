using System.Text.Json;

namespace apix.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static string RootDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".apix");

    private static string ConfigPath => Path.Combine(RootDir, "config.json");

    public static async Task<string?> GetEditorAsync()
    {
        var config = await LoadAsync();
        return config?.Editor;
    }

    public static async Task SetEditorAsync(string editor)
    {
        var config = await LoadAsync() ?? new AppConfig(null);
        var updated = config with { Editor = editor };
        Directory.CreateDirectory(RootDir);
        var json = JsonSerializer.Serialize(updated, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    public static async Task UnsetEditorAsync()
    {
        var config = await LoadAsync() ?? new AppConfig(null);
        var updated = config with { Editor = null };
        Directory.CreateDirectory(RootDir);
        var json = JsonSerializer.Serialize(updated, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    private static async Task<AppConfig?> LoadAsync()
    {
        if (!File.Exists(ConfigPath))
            return null;

        var json = await File.ReadAllTextAsync(ConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
    }

    private record AppConfig(string? Editor);
}
