using Server.Core.Interfaces;
using Server.Core.Models;

namespace SyncServer.Services;

public class FileService : IFileService
{
    private readonly IServerFileStorage _storage;
    private readonly IServerDatabase _database;

    public FileService(IServerFileStorage storage, IServerDatabase database)
    {
        _storage = storage;
        _database = database;
    }

    public async Task<SyncState> GetSyncStateAsync(CancellationToken ct)
    {
        var files = await _database.GetAllFilesAsync(ct);
        return new SyncState
        {
            Files = files,
            ServerVersion = "1.0.0"
        };
    }

    public async Task UploadFileAsync(string path, Stream content, string hash, CancellationToken ct)
    {
        // Сохраняем файл в хранилище
        await _storage.SaveFileAsync(path, content, ct);

        // Обновляем метаданные в базе данных
        var metadata = new FileMetadata
        {
            Path = path,
            Hash = hash,
            Size = content.Length, // Теперь это работает, т.к. MemoryStream поддерживает Length
            LastModified = DateTime.UtcNow
        };

        await _database.SaveFileAsync(metadata, ct);
    }

    public async Task<Stream> DownloadFileAsync(string path, CancellationToken ct)
    {
        return await _storage.ReadFileAsync(path, ct);
    }

    public async Task DeleteFileAsync(string path, CancellationToken ct)
    {
        // 1. Удаляем физический файл
        await _storage.DeleteFileAsync(path, ct);

        // 2. Удаляем запись из базы
        await _database.DeleteFileAsync(path, ct);
    }

    public async Task RenameFileAsync(string oldPath, string newPath, CancellationToken ct)
    {
        await _storage.MoveFileAsync(oldPath, newPath, ct);

        // Обновляем путь в базе данных
        var file = await _database.GetFileAsync(oldPath, ct);
        if (file != null)
        {
            await _database.DeleteFileAsync(oldPath, ct);
            file = file with { Path = newPath }; // Обновляем путь через record with
            await _database.SaveFileAsync(file, ct);
        }
    }
}