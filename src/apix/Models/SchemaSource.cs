namespace apix.Models;

public enum SchemaSourceType { Url, File }

public record SchemaSource(SchemaSourceType Type, string Value);
