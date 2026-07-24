namespace Client.Network;

using System.Net.Http.Json;
using System.Text.Json;
using Client.Core.Interfaces;
using Client.Core.Models;

public class SyncApiClient(HttpClient httpClient, string syncFolderPath) : ISyncApiClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _syncFolderPath = syncFolderPath;

    public async Task<SyncState> GetServerStateAsync(CancellationToken ct)
    {
        var response = await _httpClient.GetAsync("/api/Files/state", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SyncState>(cancellationToken: ct);
        return result ?? new SyncState();
    }

    public async Task<string> BeginTransactionAsync(CancellationToken ct)
    {
        var response = await _httpClient.PostAsync("/api/Transactions/begin", null, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TransactionResponse>(ct);
        return result?.TransactionId ?? throw new Exception("Transaction ID not returned");
    }

    public async Task CommitTransactionAsync(string transactionId, CancellationToken ct)
    {
        var response = await _httpClient.PostAsync($"/api/Transactions/{transactionId}/commit", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RollbackTransactionAsync(string transactionId, CancellationToken ct)
    {
        var response = await _httpClient.PostAsync($"/api/Transactions/{transactionId}/rollback", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadFileAsync(string path, FileState file, CancellationToken ct)
    {
        var fullPath = Path.Combine(_syncFolderPath, path);

        using var fileStream = File.OpenRead(fullPath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(fullPath));

        var url = $"/api/Files/{Uri.EscapeDataString(path)}?hash={file.Hash}";
        var response = await _httpClient.PutAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DownloadFileAsync(string path, FileState file, CancellationToken ct)
    {
        var url = $"/api/Files/{Uri.EscapeDataString(path)}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var fullPath = Path.Combine(_syncFolderPath, path);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null) Directory.CreateDirectory(directory);

        using var fileStream = File.Create(fullPath);
        await response.Content.CopyToAsync(fileStream, ct);
    }

    public async Task DeleteFileAsync(string path, CancellationToken ct)
    {
        var url = $"/api/Files/{Uri.EscapeDataString(path)}";
        var response = await _httpClient.DeleteAsync(url, ct);
        response.EnsureSuccessStatusCode();
    }
}

public record TransactionResponse
{
    public string TransactionId { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}