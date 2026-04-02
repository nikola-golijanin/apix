using System.Text.Json;
using apix.Models;

namespace apix.Services;

public static class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static string RootDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".apix");

    private static string HistoryPath(string service) =>
        Path.Combine(RootDir, "history", $"{service}.json");

    public static async Task<int> AppendAsync(string service, HistoryEntry entry)
    {
        var entries = await LoadAllAsync(service);
        var nextId = entries.Count > 0 ? entries.Max(e => e.Id) + 1 : 1;
        var withId = entry with { Id = nextId };
        entries.Add(withId);
        await SaveAsync(service, entries);
        return nextId;
    }

    public static async Task<List<HistoryEntry>> LoadAllAsync(string service)
    {
        var path = HistoryPath(service);
        if (!File.Exists(path))
            return [];

        var json = await File.ReadAllTextAsync(path);
        var root = JsonSerializer.Deserialize<HistoryRoot>(json, JsonOptions);
        return root?.Entries ?? [];
    }

    public static async Task<HistoryEntry?> FindAsync(string service, int id)
    {
        var entries = await LoadAllAsync(service);
        return entries.Find(e => e.Id == id);
    }

    private static async Task SaveAsync(string service, List<HistoryEntry> entries)
    {
        var dir = Path.Combine(RootDir, "history");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(new HistoryRoot(entries), JsonOptions);
        await File.WriteAllTextAsync(HistoryPath(service), json);
    }

    private record HistoryRoot(List<HistoryEntry> Entries);
}
