using System.Windows;
using System.Collections.ObjectModel;
using Client.Core.Models;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;

namespace SyncClient;

public partial class ConflictResolutionWindow : Window
{
    public ObservableCollection<ConflictResolutionItem> Conflicts { get; } = new();
    public List<ConflictResolution> Resolutions { get; } = new();

    public ConflictResolutionWindow(List<SyncComparison> conflicts)
    {
        InitializeComponent();
        foreach (var conflict in conflicts)
        {
            Conflicts.Add(new ConflictResolutionItem(conflict));
        }
        ConflictsDataGrid.ItemsSource = Conflicts;
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        var item = (ConflictResolutionItem)((Button)sender).DataContext;

        // Открываем диалог сохранения файла
        var dialog = new SaveFileDialog
        {
            Title = $"Сохранить '{item.Path}' с сервера как...",
            FileName = Path.GetFileNameWithoutExtension(item.Path) + "_server" + Path.GetExtension(item.Path),
            Filter = "Все файлы (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(item.Path) ?? ""
        };

        if (dialog.ShowDialog() == true)
        {
            item.Resolution = new ConflictResolution
            {
                FilePath = item.Path,
                Action = ConflictResolutionAction.DownloadFromServer,
                NewFileName = dialog.FileName  // <-- Сохраняем выбранный путь
            };

            // Визуальная обратная связь
            ((Button)sender).Content = $"✓ Сохранить как {Path.GetFileName(dialog.FileName)}";
            ((Button)sender).IsEnabled = false;
        }
    }

    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        var item = (ConflictResolutionItem)((Button)sender).DataContext;
        item.Resolution = new ConflictResolution { FilePath = item.Path, Action = ConflictResolutionAction.UploadToServer };
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var item = (ConflictResolutionItem)((Button)sender).DataContext;

        // Умное удаление: удаляем там, где файла НЕТ, чтобы синхронизировать отсутствие
        if (item.ClientDate == "файл отсутствует")
            item.Resolution = new ConflictResolution { FilePath = item.Path, Action = ConflictResolutionAction.DeleteOnServer };
        else
            item.Resolution = new ConflictResolution { FilePath = item.Path, Action = ConflictResolutionAction.DeleteOnClient };
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Resolutions.Clear();
        foreach (var item in Conflicts)
        {
            if (item.Resolution != null) Resolutions.Add(item.Resolution);
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

public class ConflictResolutionItem
{
    public string Path { get; }
    public string StatusMessage { get; }
    public string ClientDate { get; }
    public string ServerDate { get; }
    public ConflictResolution? Resolution { get; set; }

    public ConflictResolutionItem(SyncComparison conflict)
    {
        Path = conflict.Path;
        StatusMessage = conflict.ConflictMessage ?? "Неизвестная ошибка";
        ClientDate = conflict.Client != null ? conflict.Client.LastModified.ToString("yyyy-MM-dd HH:mm:ss") : "файл отсутствует";
        ServerDate = conflict.Server != null ? conflict.Server.LastModified.ToString("yyyy-MM-dd HH:mm:ss") : "файл отсутствует";
    }
}

public class ConflictResolution
{
    public string FilePath { get; set; } = string.Empty;
    public ConflictResolutionAction Action { get; set; }
    public string? NewFileName { get; set; }
}

public enum ConflictResolutionAction
{
    DownloadFromServer,
    UploadToServer,
    DeleteOnServer,   // Удалить файл на сервере
    DeleteOnClient    // Удалить файл на клиенте
}