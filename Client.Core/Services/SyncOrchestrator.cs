namespace Client.Core.Services;

using Client.Core.Interfaces;
using Client.Core.Models;

public class SyncOrchestrator(
    IClientDatabase clientDb,
    ISyncApiClient apiClient,
    ILocalFileScanner fileScanner)
{
    private readonly IClientDatabase _clientDb = clientDb;
    private readonly ISyncApiClient _apiClient = apiClient;
    private readonly ILocalFileScanner _fileScanner = fileScanner;
    private readonly SyncComparator _comparator = new();

    public async Task<SyncResult> SynchronizeAsync(string folderPath, CancellationToken ct)
    {
        try
        {
            var serverStateList = await _apiClient.GetServerStateAsync(ct);
            var serverState = serverStateList.Files.ToDictionary(f => f.Path, f => f);

            var lastSyncState = await _clientDb.GetLastSyncStateAsync(ct);
            var clientState = await _fileScanner.ScanAsync(folderPath, ct);

            var comparisons = _comparator.Compare(lastSyncState, clientState, serverState);

            var conflicts = comparisons.Where(c => c.Action == SyncAction.Conflict).ToList();
            if (conflicts.Count > 0)
            {
                return SyncResult.WithConflicts(conflicts);
            }

            var transactionId = await _apiClient.BeginTransactionAsync(ct);

            try
            {
                int processedCount = 0;

                var uploads = comparisons.Where(c => c.Action == SyncAction.UploadToServer).ToList();
                foreach (var item in uploads)
                {
                    if (item.Client != null)
                    {
                        await _apiClient.UploadFileAsync(item.Path, item.Client, ct);
                        processedCount++;
                    }
                }

                var downloads = comparisons.Where(c => c.Action == SyncAction.DownloadFromServer).ToList();
                foreach (var item in downloads)
                {
                    if (item.Server != null)
                    {
                        await _apiClient.DownloadFileAsync(item.Path, item.Server, ct);
                        processedCount++;
                    }
                }

                await _apiClient.CommitTransactionAsync(transactionId, ct);
                await _clientDb.SaveLastSyncStateAsync(serverState, ct);

                return SyncResult.Success(processedCount);
            }
            catch (Exception)
            {
                await _apiClient.RollbackTransactionAsync(transactionId, ct);
                throw;
            }
        }
        catch (Exception ex)
        {
            return SyncResult.Error($"Ошибка синхронизации: {ex.Message}");
        }
    }
}