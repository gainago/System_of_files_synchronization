namespace Server.Core.Models;

public record FileMetadata
{
    public string Path { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
}