namespace Client.FileSystem;

using System.Security.Cryptography;
using Client.Core.Interfaces;
using Client.Core.Models;

public class LocalFileScanner : ILocalFileScanner
{
    public Task<Dictionary<string, FileState>> ScanAsync(string folderPath, CancellationToken ct)
    {
        var result = new Dictionary<string, FileState>();

        if (!Directory.Exists(folderPath))
            return Task.FromResult(result);

        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden) ||
                fileInfo.Attributes.HasFlag(FileAttributes.System))
                continue;

            if (fileInfo.Name.StartsWith("~$"))
                continue;

            // Упрощённый Substring
            var relativePath = Path.GetRelativePath(folderPath, filePath).Replace('\\', '/');

            var hash = ComputeFileHash(filePath);

            result[relativePath] = new FileState
            {
                Path = relativePath,
                Hash = hash,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc
            };
        }

        return Task.FromResult(result);
    }

    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        // Используем Convert.ToHexStringLower (доступно в .NET 5+)
        return Convert.ToHexStringLower(hashBytes);
    }
}