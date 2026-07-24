namespace Client.Core.Models;

public record FileState
{
    public string Path { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
}