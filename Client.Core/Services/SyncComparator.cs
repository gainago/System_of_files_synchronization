namespace Client.Core.Services;

using Client.Core.Models;

public class SyncComparator
{
    public List<SyncComparison> Compare(
        Dictionary<string, FileState> lastSync,
        Dictionary<string, FileState> client,
        Dictionary<string, FileState> server)
    {
        var allPaths = new HashSet<string>(lastSync.Keys);
        allPaths.UnionWith(client.Keys);
        allPaths.UnionWith(server.Keys);

        var comparisons = new List<SyncComparison>(allPaths.Count);

        foreach (var path in allPaths)
        {
            lastSync.TryGetValue(path, out var lastSyncState);
            client.TryGetValue(path, out var clientState);
            server.TryGetValue(path, out var serverState);

            var action = DetermineAction(lastSyncState, clientState, serverState);

            comparisons.Add(new SyncComparison
            {
                Path = path,
                LastSync = lastSyncState,
                Client = clientState,
                Server = serverState,
                Action = action,
                ConflictMessage = action == SyncAction.Conflict
                    ? GetConflictMessage(lastSyncState, clientState, serverState)
                    : null
            });
        }

        return comparisons;
    }

    private static string GetConflictMessage(FileState? lastSync, FileState? client, FileState? server)
    {
        if (client == null && server != null)
            return "Файл отсутствует на клиенте, но есть на сервере. Скачать или удалить на сервере?";
        if (server == null && client != null)
            return "Файл отсутствует на сервере, но есть на клиенте. Загрузить на сервер или удалить локально?";
        return "Файл имеет разное содержимое на клиенте и сервере.";
    }

    private static SyncAction DetermineAction(FileState? lastSync, FileState? client, FileState? server)
    {
        var clientChanged = !AreStatesEqual(client, lastSync);
        var serverChanged = !AreStatesEqual(server, lastSync);

        // 1. Ничего не изменилось
        if (!clientChanged && !serverChanged)
            return SyncAction.None;

        // 2. Новый файл, который уже совпадает (добавлен вручную в обе папки)
        if (client != null && server != null && lastSync == null && client.Hash == server.Hash)
            return SyncAction.None;

        // 3. Файл удалён с ОБОИХ сторон
        if (client == null && server == null && lastSync != null)
            return SyncAction.None;

        // 4. КОНФЛИКТ: Файл был, но исчез на клиенте, а на сервере остался
        if (client == null && server != null && lastSync != null)
            return SyncAction.Conflict;

        // 5. КОНФЛИКТ: Файл был, но исчез на сервере, а на клиенте остался
        // Это твой сценарий: другой клиент удалил файл на сервере, а у нас он есть
        if (server == null && client != null && lastSync != null)
            return SyncAction.Conflict;

        // 6. КОНФЛИКТ: Новый файл на клиенте, но его нет на сервере
        // (возможно, был удалён другим клиентом)
        if (client != null && server == null && lastSync == null)
            return SyncAction.Conflict;

        // 7. КОНФЛИКТ: Новый файл на сервере, но его нет на клиенте
        if (server != null && client == null && lastSync == null)
            return SyncAction.Conflict;

        // 8. Конфликт содержимого: файл изменился и там, и там
        if (clientChanged && serverChanged)
        {
            if (client != null && server != null && client.Hash == server.Hash)
                return SyncAction.None;

            return SyncAction.Conflict;
        }

        // 9. Изменился только сервер → скачать
        if (serverChanged)
            return SyncAction.DownloadFromServer;

        // 10. Изменился только клиент → загрузить
        return SyncAction.UploadToServer;
    }

    private static bool AreStatesEqual(FileState? a, FileState? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Hash == b.Hash;
    }
}