namespace Server.Core.Interfaces;

using Server.Core.Models;

public interface ITransactionService
{
    Task<string> BeginTransactionAsync(string clientId, CancellationToken ct);
    Task CommitTransactionAsync(string transactionId, CancellationToken ct);
    Task RollbackTransactionAsync(string transactionId, CancellationToken ct);
    Task<Transaction?> GetTransactionAsync(string transactionId, CancellationToken ct);
}