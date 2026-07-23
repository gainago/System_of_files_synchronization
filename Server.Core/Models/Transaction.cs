namespace Server.Core.Models;

public record Transaction
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public TransactionStatus Status { get; init; }
    public List<FileOperation> Operations { get; init; } = new();
}

public enum TransactionStatus
{
    Active,
    Committed,
    RolledBack,
    Expired
}

public record FileOperation
{
    public OperationType Type { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? OldPath { get; init; }
    public string? BackupPath { get; init; }
}

public enum OperationType
{
    Upload,
    Delete,
    Rename
}