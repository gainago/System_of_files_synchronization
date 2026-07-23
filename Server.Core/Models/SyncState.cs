namespace Server.Core.Models;

public record SyncState
{
    public List<FileMetadata> Files { get; init; } = new();
    public string ServerVersion { get; init; } = "1.0.0";
}