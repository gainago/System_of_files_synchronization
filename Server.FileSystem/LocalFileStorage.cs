using Server.Core.Interfaces;

namespace Server.FileSystem;

public class LocalFileStorage : IServerFileStorage
{
    private readonly string _basePath;
    private readonly string _backupPath;

    public LocalFileStorage(string basePath, string backupPath)
    {
        _basePath = Path.GetFullPath(basePath);
        _backupPath = Path.GetFullPath(backupPath);

        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(_backupPath);
    }

    public async Task<string> SaveFileAsync(string relativePath, Stream content, CancellationToken ct)
    {
        var fullPath = GetFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;

        // ✅ Создаём подпапки если их нет
        Directory.CreateDirectory(directory);

        int retryCount = 0;
        while (retryCount < 5)
        {
            try
            {
                using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                await content.CopyToAsync(fileStream, ct);
                return fullPath;
            }
            catch (IOException)
            {
                retryCount++;
                await Task.Delay(500, ct);
            }
        }

        throw new IOException($"Не удалось сохранить файл {relativePath} после 5 попыток.");
    }

    public async Task<Stream> ReadFileAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = GetFullPath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Файл не найден: {relativePath}");

        var stream = File.OpenRead(fullPath);
        return stream;
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = GetFullPath(relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string source, string destination, CancellationToken ct)
    {
        var fullSource = GetFullPath(source);
        var fullDestination = GetFullPath(destination);

        var destDirectory = Path.GetDirectoryName(fullDestination)!;
        Directory.CreateDirectory(destDirectory);

        if (File.Exists(fullSource))
            File.Move(fullSource, fullDestination);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = GetFullPath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public async Task<string> CreateBackupAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = GetFullPath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Файл не найден для бэкапа: {relativePath}");

        var backupFileName = $"{Path.GetFileName(relativePath)}.{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var backupFullPath = Path.Combine(_backupPath, backupFileName);

        var backupDirectory = Path.GetDirectoryName(backupFullPath)!;
        Directory.CreateDirectory(backupDirectory);

        using var sourceStream = File.OpenRead(fullPath);
        using var backupStream = File.Create(backupFullPath);
        await sourceStream.CopyToAsync(backupStream, ct);

        return backupFullPath;
    }

    public Task RestoreFromBackupAsync(string backupPath, string originalPath, CancellationToken ct)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException($"Бэкап не найден: {backupPath}");

        var fullOriginalPath = GetFullPath(originalPath);
        var directory = Path.GetDirectoryName(fullOriginalPath)!;
        Directory.CreateDirectory(directory);

        File.Copy(backupPath, fullOriginalPath, overwrite: true);

        return Task.CompletedTask;
    }

    public Task DeleteBackupAsync(string backupPath, CancellationToken ct)
    {
        if (File.Exists(backupPath))
            File.Delete(backupPath);

        return Task.CompletedTask;
    }

    private string GetFullPath(string relativePath)
    {
        // Нормализуем разделители
        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_basePath, normalizedPath);
    }
}