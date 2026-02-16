using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MySQLClientShow.App.Models;
using MySQLClientShow.App.ViewModels;

namespace MySQLClientShow.App.Views;

public partial class MainWindow : Window
{
    private const string WindowTitleBase = "MySQL Client Show";
    private bool _closeAfterStop;
    private bool _stopInProgress;

    public MainWindow()
    {
        InitializeComponent();
        Title = BuildWindowTitle();

        if (OperatingSystem.IsMacOS())
        {
            TrySetMacWindowIcon();
        }

        Closing += OnClosing;
        Opened += OnOpened;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static string BuildWindowTitle()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null)
        {
            return WindowTitleBase;
        }

        return $"{WindowTitleBase} - v{version.ToString(4)}";
    }

    private void TrySetMacWindowIcon()
    {
        try
        {
            using var iconStream = AssetLoader.Open(new Uri("avares://MySQLClientShow.App/Assets/mysql-client-show.png"));
            Icon = new WindowIcon(iconStream);
        }
        catch
        {
            // Best effort icon load on macOS.
        }
    }

    private async void OnLogDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Control sourceControl ||
            sourceControl.FindAncestorOfType<DataGridRow>() is null)
        {
            return;
        }

        if (sender is not DataGrid logDataGrid ||
            logDataGrid.SelectedItem is not GeneralLogEntry selectedEntry)
        {
            return;
        }

        await OpenQueryDetailAsync(selectedEntry);
    }

    private async void OnOpenQueryDetailFromContextMenuClick(object? sender, RoutedEventArgs e)
    {
        if (ResolveContextMenuEntry(sender) is not GeneralLogEntry selectedEntry)
        {
            return;
        }

        await OpenQueryDetailAsync(selectedEntry);
    }

    private async void OnCopyQueryToClipboardFromContextMenuClick(object? sender, RoutedEventArgs e)
    {
        if (ResolveContextMenuEntry(sender) is not GeneralLogEntry selectedEntry ||
            Clipboard is null)
        {
            return;
        }

        await Clipboard.SetTextAsync(selectedEntry.SqlText);

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.NotifyStatus("Query copiata in clipboard.");
        }
    }

    private async Task OpenQueryDetailAsync(GeneralLogEntry entry)
    {
        var queryDetailWindow = new QueryDetailWindow(entry);
        await queryDetailWindow.ShowDialog(this);
    }

    private GeneralLogEntry? ResolveContextMenuEntry(object? sender)
    {
        if (sender is MenuItem menuItem)
        {
            var contextMenu = menuItem.FindAncestorOfType<ContextMenu>();
            if (contextMenu?.PlacementTarget is DataGridRow row &&
                row.DataContext is GeneralLogEntry rowEntry)
            {
                return rowEntry;
            }
        }

        var logDataGrid = this.FindControl<DataGrid>("LogDataGrid");
        return logDataGrid?.SelectedItem as GeneralLogEntry;
    }

    private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        List<GeneralLogEntry> rows;
        try
        {
            rows = GetRowsForExport();
        }
        catch (Exception ex)
        {
            viewModel.NotifyStatus($"Errore preparazione export CSV: {ex.Message}");
            return;
        }

        if (rows.Count == 0)
        {
            viewModel.NotifyStatus("Nessuna riga visibile da esportare.");
            return;
        }

        if (!StorageProvider.CanSave)
        {
            viewModel.NotifyStatus("Export CSV non disponibile: provider storage non supporta il salvataggio.");
            return;
        }

        var options = new FilePickerSaveOptions
        {
            Title = "Esporta dati griglia in CSV",
            SuggestedFileName = $"mysql-general-log-{DateTime.Now:yyyyMMdd-HHmmss}",
            DefaultExtension = "csv",
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("CSV")
                {
                    Patterns = new[] { "*.csv" },
                    MimeTypes = new[] { "text/csv" }
                }
            }
        };

        var targetFile = await StorageProvider.SaveFilePickerAsync(options);
        if (targetFile is null)
        {
            viewModel.NotifyStatus("Export CSV annullato.");
            return;
        }

        try
        {
            await WriteCsvAsync(targetFile, rows);
            viewModel.NotifyStatus($"Export CSV completato: {rows.Count} righe in {targetFile.Name}.");
        }
        catch (Exception ex)
        {
            viewModel.NotifyStatus($"Errore export CSV: {ex.Message}");
        }
    }

    private async void OnHelpClick(object? sender, RoutedEventArgs e)
    {
        var helpWindow = new HelpWindow();
        await helpWindow.ShowDialog(this);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeAfterStop || _stopInProgress)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsRunning)
        {
            return;
        }

        e.Cancel = true;
        _stopInProgress = true;

        try
        {
            await viewModel.StopPollingForExitAsync();
        }
        catch
        {
            // Best effort stop during application exit.
        }
        finally
        {
            _stopInProgress = false;
        }

        _closeAfterStop = true;
        Close();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        MaximizeIfLargerThanScreen();
    }

    private void MaximizeIfLargerThanScreen()
    {
        if (WindowState == WindowState.Maximized)
        {
            return;
        }

        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scaling = screen.Scaling > 0 ? screen.Scaling : 1d;
        var availableWidth = screen.WorkingArea.Width / scaling;
        var availableHeight = screen.WorkingArea.Height / scaling;

        var requestedWidth = !double.IsNaN(Width) ? Width : Bounds.Width;
        var requestedHeight = !double.IsNaN(Height) ? Height : Bounds.Height;

        if (requestedWidth <= 0 || requestedHeight <= 0)
        {
            return;
        }

        if (requestedWidth > availableWidth || requestedHeight > availableHeight)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private List<GeneralLogEntry> GetRowsForExport()
    {
        var logDataGrid = this.FindControl<DataGrid>("LogDataGrid");
        if (logDataGrid is null)
        {
            return new List<GeneralLogEntry>();
        }

        try
        {
            if (logDataGrid.CollectionView is IEnumerable sortedItems)
            {
                return sortedItems.OfType<GeneralLogEntry>().ToList();
            }
        }
        catch (NullReferenceException)
        {
            // DataGrid internals can be uninitialized in some lifecycle states.
        }

        if (logDataGrid.ItemsSource is IEnumerable sourceItems)
        {
            return sourceItems.OfType<GeneralLogEntry>().ToList();
        }

        return new List<GeneralLogEntry>();
    }

    private static async Task WriteCsvAsync(IStorageFile targetFile, IReadOnlyList<GeneralLogEntry> rows)
    {
        await using var stream = await targetFile.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync("Timestamp,UserHost,SQL");

        foreach (var row in rows)
        {
            var timestamp = row.EventTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
            var line = string.Join(',',
                EscapeCsvField(timestamp),
                EscapeCsvField(row.UserHost),
                EscapeCsvField(row.SqlText));

            await writer.WriteLineAsync(line);
        }
    }

    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
