using System.Data;
using MySQLClientShow.App.Models;
using MySqlConnector;

namespace MySQLClientShow.App.Services;

public sealed class MySqlGeneralLogService : IAsyncDisposable
{
    private const string EnableLogOutputSql = "SET GLOBAL log_output = 'TABLE';";
    private const string EnableGeneralLogSql = "SET GLOBAL general_log = 'ON';";
    private const string DisableGeneralLogSql = "SET GLOBAL general_log = 'OFF';";

    private const string ReadGeneralLogSql = """
                                             SELECT
                                               event_time,
                                               user_host,
                                               CAST(argument AS CHAR(65535)) AS sql_text
                                             FROM mysql.general_log
                                             WHERE command_type = 'Query'
                                               AND argument IS NOT NULL
                                               AND user_host LIKE @userHostLike
                                               AND event_time >= @fromTime
                                             ORDER BY event_time ASC;
                                             """;

    private MySqlConnection? _connection;

    public bool IsConnected => _connection is { State: ConnectionState.Open };

    public async Task ConnectAndEnableAsync(string connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        if (IsConnected)
        {
            return;
        }

        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ExecuteNonQueryAsync(connection, EnableLogOutputSql, cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, EnableGeneralLogSql, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _connection = connection;
    }

    public async Task<IReadOnlyList<GeneralLogEntry>> ReadEntriesAsync(
        DateTime fromTime,
        string userHostLike,
        CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Connection is not initialized.");
        }

        await using var command = _connection.CreateCommand();
        command.CommandText = ReadGeneralLogSql;
        command.Parameters.AddWithValue("@userHostLike", userHostLike);
        command.Parameters.AddWithValue("@fromTime", fromTime);

        var rows = new List<GeneralLogEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var eventTimeOrdinal = reader.GetOrdinal("event_time");
        var userHostOrdinal = reader.GetOrdinal("user_host");
        var sqlTextOrdinal = reader.GetOrdinal("sql_text");

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var eventTime = reader.GetDateTime(eventTimeOrdinal);
            var userHost = reader.IsDBNull(userHostOrdinal) ? string.Empty : reader.GetString(userHostOrdinal);
            var sqlText = reader.IsDBNull(sqlTextOrdinal) ? string.Empty : reader.GetString(sqlTextOrdinal);

            if (string.IsNullOrWhiteSpace(sqlText))
            {
                continue;
            }

            rows.Add(new GeneralLogEntry
            {
                EventTime = eventTime,
                UserHost = userHost,
                SqlText = sqlText
            });
        }

        return rows;
    }

    public async Task DisableAndDisconnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await ExecuteNonQueryAsync(_connection, DisableGeneralLogSql, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best effort during shutdown.
        }

        try
        {
            await _connection.CloseAsync().ConfigureAwait(false);
        }
        finally
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisableAndDisconnectAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(
        MySqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
