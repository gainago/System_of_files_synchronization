using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using Microsoft.Win32;

using Client.Core.Interfaces;
using Client.Core.Models;
using Client.Core.Services;
using Client.Network;
using Client.Storage;
using Client.FileSystem;
using System.Net.Http.Json;

namespace SyncClient;

public partial class MainWindow : Window
{
    private readonly IClientDatabase _clientDb;
    private readonly ILocalFileScanner _fileScanner;

    public MainWindow()
    {
        InitializeComponent();

        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_sync.db");
        _clientDb = new SqliteClientDatabase($"Data Source={dbPath}");
        _fileScanner = new LocalFileScanner();
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку для синхронизации" };
        if (dialog.ShowDialog() == true)
        {
            FolderPathTextBox.Text = dialog.FolderName;
            Log($"✅ Выбрана папка: {dialog.FolderName}");
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = FolderPathTextBox.Text.Trim();
        var serverUrl = ServerUrlTextBox.Text.Trim();
        var apiKey = ApiKeyTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show("Выберите существующую папку для синхронизации!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SyncButton.IsEnabled = false;
        Log("🔄 Начало процесса синхронизации...");

        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(serverUrl) };

            // Аутентификация
            Log("🔑 Выполняется аутентификация...");
            var authRequest = new { apiKey };
            var authContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(authRequest),
                Encoding.UTF8, "application/json");

            var authResponse = await httpClient.PostAsync("/api/Auth/login", authContent);

            if (!authResponse.IsSuccessStatusCode)
            {
                var err = await authResponse.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка аутентификации: {authResponse.StatusCode} - {err}");
            }

            var authResult = await authResponse.Content.ReadFromJsonAsync<AuthResponse>();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);
            Log("✅ Аутентификация успешна");

            var apiClient = new SyncApiClient(httpClient, folderPath);
            var orchestrator = new SyncOrchestrator(_clientDb, apiClient, _fileScanner);

            Log("⏳ Сравнение состояний и синхронизация...");
            var result = await orchestrator.SynchronizeAsync(folderPath, CancellationToken.None);

            if (result.Status == SyncStatus.Success)
            {
                Log($"🎉 Синхронизация успешно завершена! Обработано файлов: {result.FilesProcessed}");
            }
            else if (result.Status == SyncStatus.Conflicts)
            {
                Log($"⚠️ Обнаружены конфликты синхронизации ({result.Conflicts.Count} шт.)");

                foreach (var conflict in result.Conflicts)
                {
                    var lastSyncHash = conflict.LastSync?.Hash ?? "нет данных";
                    var clientHash = conflict.Client?.Hash ?? "файл отсутствует";
                    var serverHash = conflict.Server?.Hash ?? "файл отсутствует";

                    Log($"    {conflict.Path}:");
                    Log($"      Хэш при последней синхронизации: {lastSyncHash.Substring(0, 8)}...");
                    Log($"      Хэш на клиенте:                  {clientHash.Substring(0, 8)}...");
                    Log($"      Хэш на сервере:                  {serverHash.Substring(0, 8)}...");
                }

                var resolutionWindow = new ConflictResolutionWindow(result.Conflicts);
                if (resolutionWindow.ShowDialog() == true)
                {
                    foreach (var resolution in resolutionWindow.Resolutions)
                    {
                        switch (resolution.Action)
                        {
                            case ConflictResolutionAction.DownloadFromServer:
                                await DownloadFromServer(resolution.FilePath, null, folderPath, httpClient);
                                break;
                            case ConflictResolutionAction.UploadToServer:
                                await UploadToServer(resolution.FilePath, folderPath, httpClient);
                                break;
                            case ConflictResolutionAction.DeleteOnServer:
                                await DeleteOnServerAsync(resolution.FilePath, httpClient);
                                break;
                            case ConflictResolutionAction.DeleteOnClient:
                                await DeleteOnClientAsync(resolution.FilePath, folderPath);
                                break;
                        }
                    }

                    Log("✅ Конфликты разрешены, запускаю повторную синхронизацию...");

                    // После разрешения конфликтов запускаем новую синхронизацию
                    var secondResult = await orchestrator.SynchronizeAsync(folderPath, CancellationToken.None);

                    if (secondResult.Status == SyncStatus.Success)
                    {
                        Log($"🎉 Повторная синхронизация успешна! Обработано файлов: {secondResult.FilesProcessed}");
                    }
                    else if (secondResult.Status == SyncStatus.Conflicts)
                    {
                        Log($"️ Остались неразрешенные конфликты ({secondResult.Conflicts.Count} шт.)");
                    }
                    else
                    {
                        Log($"❌ Ошибка повторной синхронизации: {secondResult.ErrorMessage}");
                    }
                }
            }
            else if (result.Status == SyncStatus.Error)
            {
                Log($"❌ Ошибка синхронизации: {result.ErrorMessage}");
                MessageBox.Show(result.ErrorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Критическая ошибка: {ex.Message}");
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SyncButton.IsEnabled = true;
        }
    }

    private async Task DownloadFromServer(string filePath, string? newFileName, string folderPath, HttpClient httpClient)
    {
        var apiClient = new SyncApiClient(httpClient, folderPath);
        var serverState = await apiClient.GetServerStateAsync(CancellationToken.None);
        var serverFile = serverState.Files.FirstOrDefault(f => f.Path == filePath);

        if (serverFile == null)
        {
            Log($"❌ Файл {filePath} не найден на сервере");
            return;
        }

        await apiClient.DownloadFileAsync(serverFile.Path, serverFile, CancellationToken.None);

        if (!string.IsNullOrEmpty(newFileName))
        {
            var oldPath = Path.Combine(folderPath, filePath);
            File.Move(oldPath, newFileName);
            Log($"✅ Файл {filePath} скачан и сохранен как {Path.GetFileName(newFileName)}");
        }
        else
        {
            Log($"✅ Файл {filePath} скачан с сервера");
        }
    }

    private async Task UploadToServer(string filePath, string folderPath, HttpClient httpClient)
    {
        var apiClient = new SyncApiClient(httpClient, folderPath);
        var clientState = await _fileScanner.ScanAsync(folderPath, CancellationToken.None);
        var clientFile = clientState.GetValueOrDefault(filePath);

        if (clientFile == null)
        {
            Log($"❌ Файл {filePath} не найден на клиенте");
            return;
        }

        await apiClient.UploadFileAsync(clientFile.Path, clientFile, CancellationToken.None);
        Log($"✅ Файл {filePath} загружен на сервер");
    }

    private async Task KeepBothVersions(string filePath, string? newFileName, string folderPath, HttpClient httpClient)
    {
        await DownloadFromServer(filePath, newFileName, folderPath, httpClient);

        var clientState = await _fileScanner.ScanAsync(folderPath, CancellationToken.None);
        var clientFile = clientState.GetValueOrDefault(filePath);

        if (clientFile == null)
        {
            Log($"❌ Файл {filePath} не найден на клиенте");
            return;
        }

        var newFilePath = newFileName ?? Path.Combine(
            Path.GetDirectoryName(filePath) ?? "",
            $"copy_{Path.GetFileName(filePath)}"
        );

        var oldPath = Path.Combine(folderPath, filePath);
        var newPath = Path.Combine(folderPath, newFilePath);
        File.Copy(oldPath, newPath, true);

        Log($"✅ Файл {filePath} сохранен в двух версиях");
    }

    private async Task DeleteOnServerAsync(string filePath, HttpClient httpClient)
    {
        var apiClient = new SyncApiClient(httpClient, FolderPathTextBox.Text);
        await apiClient.DeleteFileAsync(filePath, CancellationToken.None);
        Log($"✅ Файл '{filePath}' удалён на сервере");
    }

    private async Task DeleteOnClientAsync(string filePath, string folderPath)
    {
        var fullPath = System.IO.Path.Combine(folderPath, filePath);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
            Log($"✅ Файл '{filePath}' удалён на клиенте");

            // Очистка пустых папок
            var dir = System.IO.Path.GetDirectoryName(fullPath);
            while (dir != null && dir != folderPath)
            {
                if (!System.IO.Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    System.IO.Directory.Delete(dir);
                    dir = System.IO.Path.GetDirectoryName(dir);
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void Log(string message)
    {
        var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        Action updateUi = () =>
        {
            LogTextBox.AppendText(logEntry);
            LogTextBox.ScrollToEnd();
        };

        if (Dispatcher.CheckAccess()) updateUi();
        else Dispatcher.Invoke(updateUi);
    }
}

public record AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}