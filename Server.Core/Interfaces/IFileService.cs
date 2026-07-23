namespace Server.Core.Interfaces;

using Server.Core.Models;

public interface IFileService
{
    Task<SyncState> GetSyncStateAsync(CancellationToken ct);
    Task UploadFileAsync(string path, Stream content, string hash, CancellationToken ct);
    Task<Stream> DownloadFileAsync(string path, CancellationToken ct);
    Task DeleteFileAsync(string path, CancellationToken ct);
    Task RenameFileAsync(string oldPath, string newPath, CancellationToken ct);
}