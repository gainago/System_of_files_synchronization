namespace Client.Core.Interfaces;

using Client.Core.Models;

public interface ILocalFileScanner
{
    /// <summary>
    /// Сканирует папку и возвращает словарь: Путь -> Состояние файла
    /// </summary>
    Task<Dictionary<string, FileState>> ScanAsync(string folderPath, CancellationToken ct);
}