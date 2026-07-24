namespace Client.Core.Interfaces;
using Client.Core.Models;

public interface IClientDatabase
{
    Task<Dictionary<string, FileState>> GetLastSyncStateAsync(CancellationToken ct);
    Task SaveLastSyncStateAsync(Dictionary<string, FileState> state, CancellationToken ct);
}