namespace Client.Core.Models;

using System.Collections.Generic;

public record SyncResult
{
    public SyncStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SyncComparison> Conflicts { get; init; } = new();
    public int FilesProcessed { get; init; }

    public static SyncResult Success(int filesProcessed) => new()
    {
        Status = SyncStatus.Success,
        FilesProcessed = filesProcessed
    };

    public static SyncResult WithConflicts(List<SyncComparison> conflicts) => new()
    {
        Status = SyncStatus.Conflicts,
        Conflicts = conflicts
    };

    public static SyncResult Error(string message) => new()
    {
        Status = SyncStatus.Error,
        ErrorMessage = message
    };
}

public enum SyncStatus
{
    Success,    // Всё прошло отлично
    Conflicts,  // Есть файлы, изменённые и там, и там
    Error       // Произошла техническая ошибка (сеть, диск и т.д.)
}
