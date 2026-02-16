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
    private const string AllClientsFilterOption = "(Tutti i client)";

    private const int MaxEntriesInMemory = 5000;
    private const int MaxDedupeKeys = 10000;

    private readonly MySqlGeneralLogService _service = new();
    private readonly ObservableCollection<GeneralLogEntry> _allEntries = new();
    private readonly HashSet<string> _dedupeSet = new(StringComparer.Ordinal);
    private readonly Queue<string> _dedupeQueue = new();

    private string _connectionString = string.Empty;
    private string _selectedClientFilter = AllClientsFilterOption;
    private string _querySearchFilter = string.Empty;
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

#if DEBUG
        SeedDebugEntries();
#endif
    }

    public ObservableCollection<GeneralLogEntry> FilteredEntries { get; } = new();
    public ObservableCollection<string> ClientFilters { get; } = new() { AllClientsFilterOption };

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
        get => string.Equals(SelectedClientFilter, AllClientsFilterOption, StringComparison.Ordinal)
            ? string.Empty
            : SelectedClientFilter;
        set => SelectedClientFilter = value;
    }

    public string SelectedClientFilter
    {
        get => _selectedClientFilter;
        set
        {
            var normalizedValue = NormalizeClientFilterOption(value);
            EnsureClientFilterOption(normalizedValue);

            if (SetProperty(ref _selectedClientFilter, normalizedValue))
            {
                OnPropertyChanged(nameof(ClientFilter));
                ApplyClientFilter();
            }
        }
    }

    public string QuerySearchFilter
    {
        get => _querySearchFilter;
        set
        {
            var normalizedValue = NormalizeQuerySearchFilter(value);
            if (SetProperty(ref _querySearchFilter, normalizedValue))
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
        QuerySearchFilter = configuration.QuerySearchFilter ?? string.Empty;
        PollingIntervalMs = configuration.PollingIntervalMs;
    }

    public AppConfiguration BuildConfiguration()
    {
        return new AppConfiguration
        {
            ConnectionString = ConnectionString,
            ClientFilter = ClientFilter,
            QuerySearchFilter = QuerySearchFilter,
            PollingIntervalMs = PollingIntervalMs
        };
    }

    public Task StopPollingForExitAsync()
    {
        return StopAsync();
    }

    public void NotifyStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        StatusMessage = message;
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
        var fromTime = DateTime.MinValue;

        try
        {
            await _service.ConnectAndEnableAsync(connectionString, cancellationToken).ConfigureAwait(false);
            fromTime = await _service.ReadServerCurrentTimestampAsync(cancellationToken).ConfigureAwait(false);
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
            finalStatus = BuildMonitorErrorMessage(ex);
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

    private static string BuildMonitorErrorMessage(Exception ex)
    {
        var message = ex.InnerException is null
            ? ex.Message
            : $"{ex.Message} | {ex.InnerException.Message}";

        if (message.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
        {
            return $"Errore monitoraggio: {message}. Verifica utente/password e host autorizzato.";
        }

        if (message.Contains("public key", StringComparison.OrdinalIgnoreCase))
        {
            return $"Errore monitoraggio: {message}. Prova `AllowPublicKeyRetrieval=True` o una connessione SSL.";
        }

        return $"Errore monitoraggio: {message}";
    }

    private void ClearBuffer()
    {
        ResetBuffer();
        ResetClientFilters();
        StatusMessage = IsRunning
            ? "Buffer e filtri client svuotati. Monitoraggio attivo."
            : "Buffer e filtri client svuotati.";
    }

#if DEBUG
    private void SeedDebugEntries()
    {
        if (_allEntries.Count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var sampleRows = new List<GeneralLogEntry>
        {
            new()
            {
                EventTime = now.AddMilliseconds(-950),
                UserHost = "debug_user@appsrv-01 [10.0.10.21]",
                SqlText = "SELECT id, name, created_at FROM customers WHERE status = 'ACTIVE' ORDER BY created_at DESC LIMIT 25;"
            },
            new()
            {
                EventTime = now.AddMilliseconds(-760),
                UserHost = "report_user@bi-node [10.0.20.35]",
                SqlText = "SELECT DATE(created_at) AS day, COUNT(*) AS total_orders FROM orders WHERE created_at >= NOW() - INTERVAL 7 DAY GROUP BY DATE(created_at) ORDER BY day;"
            },
            new()
            {
                EventTime = now.AddMilliseconds(-540),
                UserHost = "api_user@backend-02 [10.0.11.12]",
                SqlText = "UPDATE inventory SET quantity = quantity - 1, updated_at = NOW() WHERE product_id = 4123 AND warehouse_id = 3;"
            },
            new()
            {
                EventTime = now.AddMilliseconds(-310),
                UserHost = "batch_user@worker-01 [10.0.30.9]",
                SqlText = "INSERT INTO audit_log (event_type, payload, created_at) VALUES ('ORDER_SYNC', '{\"orderId\":10294,\"status\":\"completed\"}', NOW());"
            },
            new()
            {
                EventTime = now.AddMilliseconds(-120),
                UserHost = "admin@localhost [127.0.0.1]",
                SqlText = "DELETE FROM session_tokens WHERE expires_at < NOW() - INTERVAL 30 DAY;"
            }
        };

        AppendEntries(sampleRows);
        StatusMessage = "Modalita debug: caricati 5 dati di esempio.";
    }
#endif

    private void AppendEntries(IReadOnlyList<GeneralLogEntry> rows)
    {
        var hasNewRows = false;
        var selectedFilter = SelectedClientFilter;
        var querySearchTerms = ParseQuerySearchTerms(QuerySearchFilter);

        foreach (var row in rows)
        {
            if (ShouldIgnore(row.SqlText))
            {
                continue;
            }

            if (!MatchesFilters(row, selectedFilter, querySearchTerms))
            {
                continue;
            }

            EnsureClientFilterOption(row.UserHost);

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

    private void ResetClientFilters()
    {
        ClientFilters.Clear();
        ClientFilters.Add(AllClientsFilterOption);
        SelectedClientFilter = AllClientsFilterOption;
    }

    private void ApplyClientFilter()
    {
        FilteredEntries.Clear();

        var selectedFilter = SelectedClientFilter;
        var querySearchTerms = ParseQuerySearchTerms(QuerySearchFilter);
        var matchingEntries = new List<GeneralLogEntry>();

        foreach (var entry in _allEntries)
        {
            if (MatchesFilters(entry, selectedFilter, querySearchTerms))
            {
                matchingEntries.Add(entry);
            }
        }

        matchingEntries.Sort(static (left, right) => right.EventTime.CompareTo(left.EventTime));
        foreach (var entry in matchingEntries)
        {
            FilteredEntries.Add(entry);
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

    private static string NormalizeClientFilterOption(string? option)
    {
        return string.IsNullOrWhiteSpace(option) ? AllClientsFilterOption : option.Trim();
    }

    private static string NormalizeQuerySearchFilter(string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) ? string.Empty : filter.Trim();
    }

    private static bool MatchesFilters(
        GeneralLogEntry entry,
        string selectedFilter,
        IReadOnlyList<string> querySearchTerms)
    {
        var includeAll = string.Equals(selectedFilter, AllClientsFilterOption, StringComparison.Ordinal);
        var matchesClient = includeAll ||
                            string.Equals(entry.UserHost, selectedFilter, StringComparison.OrdinalIgnoreCase);
        if (!matchesClient)
        {
            return false;
        }

        if (querySearchTerms.Count == 0)
        {
            return true;
        }

        foreach (var term in querySearchTerms)
        {
            if (entry.SqlText.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ParseQuerySearchTerms(string querySearchFilter)
    {
        if (string.IsNullOrWhiteSpace(querySearchFilter))
        {
            return Array.Empty<string>();
        }

        return querySearchFilter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private void EnsureClientFilterOption(string? option)
    {
        var normalizedOption = NormalizeClientFilterOption(option);
        if (string.Equals(normalizedOption, AllClientsFilterOption, StringComparison.Ordinal))
        {
            return;
        }

        for (var i = 0; i < ClientFilters.Count; i++)
        {
            if (string.Equals(ClientFilters[i], normalizedOption, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var insertAt = 1;
        while (insertAt < ClientFilters.Count &&
               string.Compare(ClientFilters[insertAt], normalizedOption, StringComparison.OrdinalIgnoreCase) < 0)
        {
            insertAt++;
        }

        ClientFilters.Insert(insertAt, normalizedOption);
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
