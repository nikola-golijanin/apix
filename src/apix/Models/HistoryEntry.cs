namespace apix.Models;

public record HistoryEntry(
    int Id,
    DateTimeOffset Timestamp,
    string Method,
    string Url,
    string? OperationId,
    Dictionary<string, string> RequestHeaders,
    string? RequestBody,
    int StatusCode,
    string StatusText,
    Dictionary<string, string> ResponseHeaders,
    string? ResponseBody,
    long DurationMs
);
