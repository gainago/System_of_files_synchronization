namespace Client.Storage;

using System.Text.Json;
using Client.Core.Interfaces;
using Client.Core.Models;
using Microsoft.Data.Sqlite;

public class SqliteClientDatabase : IClientDatabase, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteClientDatabase(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);

        // 👇 ВОТ ЭТОЙ СТРОКИ НЕ ХВАТАЛО! 👇
        _connection.Open();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS LastSyncState (
                Path TEXT PRIMARY KEY,
                FileStateJson TEXT NOT NULL
            );
        ";
        command.ExecuteNonQuery();
    }

    public async Task<Dictionary<string, FileState>> GetLastSyncStateAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, FileState>();

        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Path, FileStateJson FROM LastSyncState";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var path = reader.GetString(0);
            var json = reader.GetString(1);
            var fileState = JsonSerializer.Deserialize<FileState>(json);

            if (fileState != null)
                result[path] = fileState;
        }

        return result;
    }

    public async Task SaveLastSyncStateAsync(Dictionary<string, FileState> state, CancellationToken ct)
    {
        // Очищаем старое состояние
        var clearCommand = _connection.CreateCommand();
        clearCommand.CommandText = "DELETE FROM LastSyncState";
        await clearCommand.ExecuteNonQueryAsync(ct);

        // Сохраняем новое состояние
        foreach (var (path, fileState) in state)
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO LastSyncState (Path, FileStateJson)
                VALUES (@path, @json)";

            command.Parameters.AddWithValue("@path", path);
            command.Parameters.AddWithValue("@json", JsonSerializer.Serialize(fileState));

            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}