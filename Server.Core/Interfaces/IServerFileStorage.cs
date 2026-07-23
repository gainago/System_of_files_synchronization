namespace Server.Core.Interfaces;

public interface IServerFileStorage
{
    Task<string> SaveFileAsync(string relativePath, Stream content, CancellationToken ct);
    Task<Stream> ReadFileAsync(string relativePath, CancellationToken ct);
    Task DeleteFileAsync(string relativePath, CancellationToken ct);
    Task MoveFileAsync(string source, string destination, CancellationToken ct);
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct);
    Task<string> CreateBackupAsync(string relativePath, CancellationToken ct);
    Task RestoreFromBackupAsync(string backupPath, string originalPath, CancellationToken ct);
    Task DeleteBackupAsync(string backupPath, CancellationToken ct);
}