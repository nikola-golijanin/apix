namespace apix.Models;

public record ServiceEntry(
    string Name,
    string BaseUrl,
    SchemaSource SchemaSource,
    int EndpointCount,
    DateTimeOffset ImportedAt
);
