using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MySQLClientShow.App.Configuration;
using MySQLClientShow.App.Models;
using MySQLClientShow.App.Services;

namespace MySQLClientShow.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    public const int DefaultPollingIntervalMs = 1000;
    public const int MinPollingIntervalMs = 200;
    public const int MaxPollingIntervalMs = 60000;

    private const int MaxEntriesInMemory = 5000;
    private const int MaxDedupeKeys = 10000;

    private readonly MySqlGeneralLogService _service = new();
    private readonly ObservableCollection<GeneralLogEntry> _allEntries = new();
    private readonly HashSet<string> _dedupeSet = new(StringComparer.Ordinal);
    private readonly Queue<string> _dedupeQueue = new();

    private string _connectionString = string.Empty;
    private string _clientFilter = string.Empty;
    private string _statusMessage = "Pronto. Inserisci la connection string.";
    private int _pollingIntervalMs = DefaultPollingIntervalMs;
    private bool _isRunning;
    private bool _isStopping;
    private int _totalRows;
    private int _visibleRows;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public MainWindowViewModel()
    {
        StartCommand = new AsyncRelayCommand(StartAsync, CanStart);
        StopCommand = new AsyncRelayCommand(StopAsync, CanStop);
        ClearCommand = new RelayCommand(ClearBuffer);
    }

    public ObservableCollection<GeneralLogEntry> FilteredEntries { get; } = new();

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand StopCommand { get; }

    public RelayCommand ClearCommand { get; }

    public string ConnectionString
    {
        get => _connectionString;
        set
        {
            if (SetProperty(ref _connectionString, value))
            {
                StartCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ClientFilter
    {
        get => _clientFilter;
        set
        {
            if (SetProperty(ref _clientFilter, value))
            {
                ApplyClientFilter();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int PollingIntervalMs
    {
        get => _pollingIntervalMs;
        set
        {
            var normalizedValue = NormalizePollingInterval(value);
            SetProperty(ref _pollingIntervalMs, normalizedValue);
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                StartCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalRows
    {
        get => _totalRows;
        private set => SetProperty(ref _totalRows, value);
    }

    public int VisibleRows
    {
        get => _visibleRows;
        private set => SetProperty(ref _visibleRows, value);
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRunning)
        {
            await StopAsync().ConfigureAwait(false);
        }

        await _service.DisposeAsync().ConfigureAwait(false);
    }

    public void ApplyConfiguration(AppConfiguration configuration)
    {
        ConnectionString = configuration.ConnectionString ?? string.Empty;
        ClientFilter = configuration.ClientFilter ?? string.Empty;
        PollingIntervalMs = configuration.PollingIntervalMs;
    }

    public AppConfiguration BuildConfiguration()
    {
        return new AppConfiguration
        {
            ConnectionString = ConnectionString,
            ClientFilter = ClientFilter,
            PollingIntervalMs = PollingIntervalMs
        };
    }

    public Task StopPollingForExitAsync()
    {
        return StopAsync();
    }

    private bool CanStart() => !IsRunning && !string.IsNullOrWhiteSpace(ConnectionString);

    private bool CanStop() => IsRunning && !_isStopping;

    private Task StartAsync()
    {
        if (!CanStart())
        {
            return Task.CompletedTask;
        }

        ResetBuffer();
        IsRunning = true;
        StatusMessage = "Connessione al server MySQL...";

        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => RunPollingSessionAsync(ConnectionString, _pollingCts.Token));
        return Task.CompletedTask;
    }

    private async Task StopAsync()
    {
        if (_pollingTask is null || _pollingCts is null)
        {
            return;
        }

        if (_isStopping)
        {
            try
            {
                await _pollingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected while another stop is already in progress.
            }

            return;
        }

        _isStopping = true;
        StopCommand.NotifyCanExecuteChanged();
        StatusMessage = "Arresto monitoraggio...";

        _pollingCts.Cancel();

        try
        {
            await _pollingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during stop.
        }
        finally
        {
            _isStopping = false;
            RunOnUiThread(StopCommand.NotifyCanExecuteChanged);
        }
    }

    private async Task RunPollingSessionAsync(string connectionString, CancellationToken cancellationToken)
    {
        var finalStatus = "Monitoraggio fermo.";
        var hasError = false;
        var fromTime = DateTime.UtcNow;

        try
        {
            await _service.ConnectAndEnableAsync(connectionString, cancellationToken).ConfigureAwait(false);
            RunOnUiThread(() =>
                StatusMessage = $"Monitoraggio attivo. Polling: {PollingIntervalMs} ms.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var rows = await _service.ReadEntriesAsync(fromTime, "%", cancellationToken).ConfigureAwait(false);

                if (rows.Count > 0)
                {
                    fromTime = rows[^1].EventTime;

                    RunOnUiThread(() =>
                    {
                        AppendEntries(rows);
                        StatusMessage = $"Monitoraggio attivo. Ultimo evento: {fromTime:yyyy-MM-dd HH:mm:ss.ffffff}";
                    });
                }

                var pollingDelay = TimeSpan.FromMilliseconds(PollingIntervalMs);
                await Task.Delay(pollingDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stop requested by user.
        }
        catch (Exception ex)
        {
            hasError = true;
            finalStatus = $"Errore monitoraggio: {ex.Message}";
        }
        finally
        {
            try
            {
                await _service.DisableAndDisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (!hasError)
            {
                hasError = true;
                finalStatus = $"Errore durante chiusura: {ex.Message}";
            }

            RunOnUiThread(() =>
            {
                IsRunning = false;
                _isStopping = false;
                StopCommand.NotifyCanExecuteChanged();
                StatusMessage = hasError ? finalStatus : "Monitoraggio fermo.";
            });

            _pollingCts?.Dispose();
            _pollingCts = null;
            _pollingTask = null;
        }
    }

    private void ClearBuffer()
    {
        ResetBuffer();
        StatusMessage = IsRunning ? "Buffer svuotato. Monitoraggio attivo." : "Buffer svuotato.";
    }

    private void AppendEntries(IReadOnlyList<GeneralLogEntry> rows)
    {
        var hasNewRows = false;

        foreach (var row in rows)
        {
            if (ShouldIgnore(row.SqlText))
            {
                continue;
            }

            var key = $"{row.EventTime.Ticks}|{row.UserHost}|{row.SqlText}";
            if (!_dedupeSet.Add(key))
            {
                continue;
            }

            _dedupeQueue.Enqueue(key);
            _allEntries.Add(row);
            hasNewRows = true;
        }

        if (!hasNewRows)
        {
            return;
        }

        while (_allEntries.Count > MaxEntriesInMemory)
        {
            _allEntries.RemoveAt(0);
        }

        while (_dedupeQueue.Count > MaxDedupeKeys)
        {
            var keyToRemove = _dedupeQueue.Dequeue();
            _dedupeSet.Remove(keyToRemove);
        }

        ApplyClientFilter();
    }

    private void ResetBuffer()
    {
        _allEntries.Clear();
        FilteredEntries.Clear();
        _dedupeSet.Clear();
        _dedupeQueue.Clear();
        UpdateCounters();
    }

    private void ApplyClientFilter()
    {
        FilteredEntries.Clear();

        var filter = ClientFilter.Trim();
        foreach (var entry in _allEntries)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                entry.UserHost.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredEntries.Add(entry);
            }
        }

        UpdateCounters();
    }

    private void UpdateCounters()
    {
        TotalRows = _allEntries.Count;
        VisibleRows = FilteredEntries.Count;
    }

    private static bool ShouldIgnore(string sqlText)
    {
        if (sqlText.Contains("FROM mysql.general_log", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (sqlText.StartsWith("SET GLOBAL general_log", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return sqlText.StartsWith("SET GLOBAL log_output", StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizePollingInterval(int value)
    {
        if (value < MinPollingIntervalMs)
        {
            return MinPollingIntervalMs;
        }

        if (value > MaxPollingIntervalMs)
        {
            return MaxPollingIntervalMs;
        }

        return value;
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
