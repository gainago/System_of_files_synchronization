namespace Client.Core.Models;

public record SyncComparison
{
    public string Path { get; init; } = string.Empty;
    public FileState? LastSync { get; init; }
    public FileState? Client { get; init; }
    public FileState? Server { get; init; }
    public SyncAction Action { get; init; }
    public string? ConflictMessage { get; init; }
}

public enum SyncAction
{
    None,
    UploadToServer,
    DownloadFromServer,
    Conflict
}