using MySQLClientShow.App.Models;

namespace MySQLClientShow.Tests;

public class GeneralLogEntryTests
{
    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var eventTime = new DateTime(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc);
        var entry = new GeneralLogEntry
        {
            EventTime = eventTime,
            UserHost = "admin@localhost [127.0.0.1]",
            SqlText = "SELECT 1"
        };

        Assert.Equal(eventTime, entry.EventTime);
        Assert.Equal("admin@localhost [127.0.0.1]", entry.UserHost);
        Assert.Equal("SELECT 1", entry.SqlText);
    }

    [Fact]
    public void EventTime_PreservesMicrosecondPrecision()
    {
        var eventTime = new DateTime(2026, 2, 16, 12, 30, 45, 123, DateTimeKind.Utc).AddTicks(4567);
        var entry = new GeneralLogEntry
        {
            EventTime = eventTime,
            UserHost = "user@host",
            SqlText = "SELECT NOW()"
        };

        Assert.Equal(eventTime.Ticks, entry.EventTime.Ticks);
    }

    [Fact]
    public void SqlText_CanContainComplexQueries()
    {
        var complexSql = "SELECT c.id, c.name, COUNT(o.id) FROM customers c LEFT JOIN orders o ON c.id = o.customer_id WHERE c.status = 'active' GROUP BY c.id, c.name ORDER BY c.name ASC;";

        var entry = new GeneralLogEntry
        {
            EventTime = DateTime.UtcNow,
            UserHost = "app@server",
            SqlText = complexSql
        };

        Assert.Equal(complexSql, entry.SqlText);
    }
}
