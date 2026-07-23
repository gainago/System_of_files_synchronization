using Microsoft.Data.Sqlite;
using Server.Core.Interfaces;

// Псевдонимы для разрешения конфликта имен с System.Transactions
using SyncTransaction = Server.Core.Models.Transaction;
using SyncTransactionStatus = Server.Core.Models.TransactionStatus;
using SyncFileOperation = Server.Core.Models.FileOperation;
using SyncFileMetadata = Server.Core.Models.FileMetadata;
using SyncOperationType = Server.Core.Models.OperationType;

namespace Server.Storage;

public class SqliteDatabase : IServerDatabase, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDatabase(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Files (
                Path TEXT PRIMARY KEY,
                Hash TEXT NOT NULL,
                Size INTEGER NOT NULL,
                LastModified TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                ClientId TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                Status INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Operations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TransactionId TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Path TEXT NOT NULL,
                OldPath TEXT,
                BackupPath TEXT,
                FOREIGN KEY (TransactionId) REFERENCES Transactions(Id)
            );
        ";
        command.ExecuteNonQuery();
    }

    public async Task<List<SyncFileMetadata>> GetAllFilesAsync(CancellationToken ct)
    {
        var files = new List<SyncFileMetadata>();
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Path, Hash, Size, LastModified FROM Files";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            files.Add(new SyncFileMetadata
            {
                Path = reader.GetString(0),
                Hash = reader.GetString(1),
                Size = reader.GetInt64(2),
                LastModified = DateTime.Parse(reader.GetString(3))
            });
        }

        return files;
    }

    public async Task<SyncFileMetadata?> GetFileAsync(string path, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Path, Hash, Size, LastModified FROM Files WHERE Path = @path";
        command.Parameters.AddWithValue("@path", path);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new SyncFileMetadata
            {
                Path = reader.GetString(0),
                Hash = reader.GetString(1),
                Size = reader.GetInt64(2),
                LastModified = DateTime.Parse(reader.GetString(3))
            };
        }

        return null;
    }

    public async Task SaveFileAsync(SyncFileMetadata file, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Files (Path, Hash, Size, LastModified)
            VALUES (@path, @hash, @size, @lastModified)";
        command.Parameters.AddWithValue("@path", file.Path);
        command.Parameters.AddWithValue("@hash", file.Hash);
        command.Parameters.AddWithValue("@size", file.Size);
        command.Parameters.AddWithValue("@lastModified", file.LastModified.ToString("O"));

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteFileAsync(string path, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM Files WHERE Path = @path";
        command.Parameters.AddWithValue("@path", path);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveTransactionAsync(SyncTransaction transaction, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Transactions (Id, ClientId, StartTime, Status)
            VALUES (@id, @clientId, @startTime, @status)";
        command.Parameters.AddWithValue("@id", transaction.Id);
        command.Parameters.AddWithValue("@clientId", transaction.ClientId);
        command.Parameters.AddWithValue("@startTime", transaction.StartTime.ToString("O"));
        command.Parameters.AddWithValue("@status", (int)transaction.Status);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<SyncTransaction?> GetTransactionAsync(string id, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, ClientId, StartTime, Status FROM Transactions WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new SyncTransaction
            {
                Id = reader.GetString(0),
                ClientId = reader.GetString(1),
                StartTime = DateTime.Parse(reader.GetString(2)),
                Status = (SyncTransactionStatus)reader.GetInt32(3)
            };
        }

        return null;
    }

    public async Task UpdateTransactionStatusAsync(string id, SyncTransactionStatus status, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "UPDATE Transactions SET Status = @status WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@status", (int)status);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task AddOperationAsync(string transactionId, SyncFileOperation operation, CancellationToken ct)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Operations (TransactionId, Type, Path, OldPath, BackupPath)
            VALUES (@transactionId, @type, @path, @oldPath, @backupPath)";
        command.Parameters.AddWithValue("@transactionId", transactionId);
        command.Parameters.AddWithValue("@type", (int)operation.Type);
        command.Parameters.AddWithValue("@path", operation.Path);
        command.Parameters.AddWithValue("@oldPath", operation.OldPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@backupPath", operation.BackupPath ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<SyncFileOperation>> GetOperationsAsync(string transactionId, CancellationToken ct)
    {
        var operations = new List<SyncFileOperation>();
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Type, Path, OldPath, BackupPath FROM Operations WHERE TransactionId = @transactionId";
        command.Parameters.AddWithValue("@transactionId", transactionId);

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            operations.Add(new SyncFileOperation
            {
                Type = (SyncOperationType)reader.GetInt32(0),
                Path = reader.GetString(1),
                OldPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                BackupPath = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return operations;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}