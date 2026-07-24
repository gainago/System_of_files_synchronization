namespace Client.Core.Interfaces;
using Client.Core.Models;

public interface ISyncApiClient
{
    Task<SyncState> GetServerStateAsync(CancellationToken ct);
    Task<string> BeginTransactionAsync(CancellationToken ct);
    Task CommitTransactionAsync(string transactionId, CancellationToken ct);
    Task RollbackTransactionAsync(string transactionId, CancellationToken ct);
    Task UploadFileAsync(string path, FileState file, CancellationToken ct);
    Task DownloadFileAsync(string path, FileState file, CancellationToken ct);

    Task DeleteFileAsync(string path, CancellationToken ct);
}

public record SyncState
{
    public List<FileState> Files { get; init; } = new();
    public string ServerVersion { get; init; } = string.Empty;
}