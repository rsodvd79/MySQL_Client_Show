namespace MySQLClientShow.App.Models;

public sealed class GeneralLogEntry
{
    public required DateTime EventTime { get; init; }

    public required string UserHost { get; init; }

    public required string SqlText { get; init; }
}
