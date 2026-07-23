namespace Server.Core.Interfaces;

using Server.Core.Models;

public interface IServerDatabase
{
    // Файлы
    Task<List<FileMetadata>> GetAllFilesAsync(CancellationToken ct);
    Task<FileMetadata?> GetFileAsync(string path, CancellationToken ct);
    Task SaveFileAsync(FileMetadata file, CancellationToken ct);
    Task DeleteFileAsync(string path, CancellationToken ct);

    // Транзакции
    Task SaveTransactionAsync(Transaction transaction, CancellationToken ct);
    Task<Transaction?> GetTransactionAsync(string id, CancellationToken ct);
    Task UpdateTransactionStatusAsync(string id, TransactionStatus status, CancellationToken ct);

    // Операции в транзакции
    Task AddOperationAsync(string transactionId, FileOperation operation, CancellationToken ct);
    Task<List<FileOperation>> GetOperationsAsync(string transactionId, CancellationToken ct);
}