using Server.Core.Interfaces;
using Server.Core.Models;

namespace SyncServer.Services;

public class TransactionService : ITransactionService
{
    private readonly IServerDatabase _database;
    private readonly IServerFileStorage _storage;

    public TransactionService(IServerDatabase database, IServerFileStorage storage)
    {
        _database = database;
        _storage = storage;
    }

    public async Task<string> BeginTransactionAsync(string clientId, CancellationToken ct)
    {
        var transactionId = Guid.NewGuid().ToString();

        var transaction = new Transaction
        {
            Id = transactionId,
            ClientId = clientId,
            StartTime = DateTime.UtcNow,
            Status = TransactionStatus.Active,
            Operations = new List<FileOperation>()
        };

        await _database.SaveTransactionAsync(transaction, ct);
        return transactionId;
    }

    public async Task CommitTransactionAsync(string transactionId, CancellationToken ct)
    {
        var transaction = await _database.GetTransactionAsync(transactionId, ct);
        if (transaction == null || transaction.Status != TransactionStatus.Active)
            throw new InvalidOperationException("Транзакция не найдена или не активна");

        await _database.UpdateTransactionStatusAsync(transactionId, TransactionStatus.Committed, ct);

        var operations = await _database.GetOperationsAsync(transactionId, ct);
        foreach (var op in operations)
        {
            if (!string.IsNullOrEmpty(op.BackupPath))
            {
                await _storage.DeleteBackupAsync(op.BackupPath, ct);
            }
        }
    }

    public async Task RollbackTransactionAsync(string transactionId, CancellationToken ct)
    {
        var transaction = await _database.GetTransactionAsync(transactionId, ct);
        if (transaction == null || transaction.Status != TransactionStatus.Active)
            throw new InvalidOperationException("Транзакция не найдена или не активна");

        var operations = await _database.GetOperationsAsync(transactionId, ct);
        operations.Reverse();

        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case OperationType.Upload:
                    await _storage.DeleteFileAsync(op.Path, ct);
                    await _database.DeleteFileAsync(op.Path, ct);
                    break;

                case OperationType.Delete:
                    if (!string.IsNullOrEmpty(op.BackupPath))
                    {
                        await _storage.RestoreFromBackupAsync(op.BackupPath, op.Path, ct);
                    }
                    break;

                case OperationType.Rename:
                    if (!string.IsNullOrEmpty(op.OldPath))
                    {
                        await _storage.MoveFileAsync(op.Path, op.OldPath, ct);
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(op.BackupPath))
            {
                await _storage.DeleteBackupAsync(op.BackupPath, ct);
            }
        }

        await _database.UpdateTransactionStatusAsync(transactionId, TransactionStatus.RolledBack, ct);
    }

    public async Task<Transaction?> GetTransactionAsync(string transactionId, CancellationToken ct)
    {
        return await _database.GetTransactionAsync(transactionId, ct);
    }
}