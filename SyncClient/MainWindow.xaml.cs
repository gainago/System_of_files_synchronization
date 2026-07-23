using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SyncClient;

public partial class MainWindow : Window
{
    private FileSystemWatcher? _watcher;
    private HttpClient? _httpClient;
    private string? _authToken;
    private bool _isSyncing = false;
    private string _syncFolderPath = string.Empty;

    // Защита от спама событий FileSystemWatcher
    private readonly ConcurrentDictionary<string, DateTime> _lastProcessed = new();
    private const int DebounceMs = 2000; // Игнорировать повторные события в течение 2 секунд

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку для синхронизации" };
        if (dialog.ShowDialog() == true)
        {
            FolderPathTextBox.Text = dialog.FolderName;
            Log($"Выбрана папка: {dialog.FolderName}");
        }
    }

    private async void StartSync_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FolderPathTextBox.Text) || !Directory.Exists(FolderPathTextBox.Text))
        {
            MessageBox.Show("Выберите существующую папку для синхронизации!");
            return;
        }

        try
        {
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            _isSyncing = true;
            _syncFolderPath = FolderPathTextBox.Text;
            _lastProcessed.Clear();

            _httpClient = new HttpClient { BaseAddress = new Uri(ServerUrlTextBox.Text) };
            await AuthenticateAsync();

            _watcher = new FileSystemWatcher(_syncFolderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _watcher.Created += Watcher_Created;
            _watcher.Changed += Watcher_Changed;
            _watcher.Deleted += Watcher_Deleted;
            _watcher.EnableRaisingEvents = true;

            Log("✅ Синхронизация запущена");
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка запуска: {ex.Message}");
            StopSync_Click(sender, e);
        }
    }

    private void StopSync_Click(object sender, RoutedEventArgs e)
    {
        _isSyncing = false;
        _watcher?.Dispose();
        _httpClient?.Dispose();
        _authToken = null;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        Log("⏹️ Синхронизация остановлена");
    }

    private void Watcher_Created(object sender, FileSystemEventArgs e) => ProcessFileEvent(e.FullPath, "Created");
    private void Watcher_Changed(object sender, FileSystemEventArgs e) => ProcessFileEvent(e.FullPath, "Changed");
    private void Watcher_Deleted(object sender, FileSystemEventArgs e) => ProcessFileEvent(e.FullPath, "Deleted");

    private void ProcessFileEvent(string fullPath, string eventType)
    {
        if (!_isSyncing) return;
        if (Path.GetFileName(fullPath).StartsWith("~$")) return; // Игнорируем временные файлы Office

        var now = DateTime.UtcNow;

        // Debounce: если событие для этого файла уже было недавно, игнорируем его
        if (_lastProcessed.TryGetValue(fullPath, out var lastTime) && (now - lastTime).TotalMilliseconds < DebounceMs)
        {
            return;
        }

        _lastProcessed[fullPath] = now;
        Task.Run(async () =>
        {
            try
            {
                if (eventType == "Deleted")
                    await HandleFileDeletedAsync(fullPath);
                else
                    await HandleFileModifiedAsync(fullPath);
            }
            finally
            {
                // Разрешаем обработку этого файла снова после завершения
                _lastProcessed.TryRemove(fullPath, out _);
            }
        });
    }

    private async Task HandleFileModifiedAsync(string fullPath)
    {
        try
        {
            await Task.Delay(1000); // Даем ОС время завершить запись
            if (!File.Exists(fullPath)) return;

            await WaitForFileReadyAsync(fullPath);
            await UploadFileWithRetryAsync(fullPath, 3);
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка {Path.GetFileName(fullPath)}: {ex.Message}");
        }
    }

    private async Task HandleFileDeletedAsync(string fullPath)
    {
        try
        {
            var relativePath = GetRelativePath(fullPath);
            await DeleteFileAsync(relativePath);
            Log($"✅ Удален на сервере: {relativePath}");
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка удаления {Path.GetFileName(fullPath)}: {ex.Message}");
        }
    }

    private async Task WaitForFileReadyAsync(string filePath)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return; // Файл доступен
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
        }
        throw new IOException("Файл заблокирован более 2.5 секунд");
    }

    private async Task UploadFileWithRetryAsync(string fullPath, int maxRetries)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var relativePath = GetRelativePath(fullPath);
                var hash = ComputeFileHash(fullPath);
                await UploadFileAsync(relativePath, fullPath, hash);
                Log($"✅ Загружен: {relativePath}");
                return;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) throw;
                Log($"⚠️ Попытка {i + 1} не удалась: {ex.Message}. Повтор...");
                await Task.Delay(1000 * (i + 1));
            }
        }
    }

    private async Task AuthenticateAsync()
    {
        var request = new { apiKey = ApiKeyTextBox.Text };
        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _httpClient!.PostAsync("/api/Auth/login", content);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        _authToken = result?.Token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
        Log("🔑 Аутентификация успешна");
    }

    private async Task UploadFileAsync(string relativePath, string fullPath, string hash)
    {
        byte[] fileBytes;
        using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var ms = new MemoryStream())
        {
            await fs.CopyToAsync(ms);
            fileBytes = ms.ToArray();
        }

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(fileBytes), "file", Path.GetFileName(fullPath));

        var url = $"/api/Files/{Uri.EscapeDataString(relativePath)}?hash={hash}";
        Log($"📤 Отправка: {url} ({fileBytes.Length} байт)");

        var response = await _httpClient!.PutAsync(url, content);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task DeleteFileAsync(string relativePath)
    {
        var response = await _httpClient!.DeleteAsync($"/api/Files/{Uri.EscapeDataString(relativePath)}");
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private string GetRelativePath(string fullPath)
    {
        var basePath = _syncFolderPath.TrimEnd('\\', '/');
        return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
            ? fullPath.Substring(basePath.Length + 1).Replace('\\', '/')
            : Path.GetFileName(fullPath);
    }

    private string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
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

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}