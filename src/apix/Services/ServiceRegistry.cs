using System.Text.Json;
using apix.Models;

namespace apix.Services;

public class ServiceRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static string RootDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".apix");

    private static string RegistryPath => Path.Combine(RootDir, "services.json");

    private static string SchemaPath(string name) =>
        Path.Combine(RootDir, "schemas", $"{name}.json");

    public static async Task UpsertAsync(ServiceEntry entry, byte[] schemaBytes)
    {
        Directory.CreateDirectory(Path.Combine(RootDir, "schemas"));

        await File.WriteAllBytesAsync(SchemaPath(entry.Name), schemaBytes);

        var entries = await LoadAllAsync();
        var index = entries.FindIndex(e => string.Equals(e.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            entries[index] = entry;
        else
            entries.Add(entry);

        await SaveRegistryAsync(entries);
    }

    public static async Task<List<ServiceEntry>> LoadAllAsync()
    {
        if (!File.Exists(RegistryPath))
            return [];

        var json = await File.ReadAllTextAsync(RegistryPath);
        var root = JsonSerializer.Deserialize<RegistryRoot>(json, JsonOptions);
        return root?.Services ?? [];
    }

    public static async Task<ServiceEntry?> FindAsync(string name)
    {
        var entries = await LoadAllAsync();
        return entries.Find(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static Stream OpenSchema(string name) =>
        File.OpenRead(SchemaPath(name));

    public static async Task RemoveAsync(string name)
    {
        var entries = await LoadAllAsync();
        entries.RemoveAll(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        await SaveRegistryAsync(entries);

        var schemaPath = SchemaPath(name);
        if (File.Exists(schemaPath))
            File.Delete(schemaPath);
    }

    private static async Task SaveRegistryAsync(List<ServiceEntry> entries)
    {
        Directory.CreateDirectory(RootDir);
        var json = JsonSerializer.Serialize(new RegistryRoot(entries), JsonOptions);
        await File.WriteAllTextAsync(RegistryPath, json);
    }

    private record RegistryRoot(List<ServiceEntry> Services);
}
