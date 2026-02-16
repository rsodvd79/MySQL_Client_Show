using MySQLClientShow.App.Utilities;

namespace MySQLClientShow.Tests;

public class SqlQueryFormatterTests
{
    [Fact]
    public void Format_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SqlQueryFormatter.Format(null));
    }

    [Fact]
    public void Format_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SqlQueryFormatter.Format(""));
    }

    [Fact]
    public void Format_WhitespaceOnlyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SqlQueryFormatter.Format("   "));
    }

    [Fact]
    public void Format_SimpleSelect_FormatsCorrectly()
    {
        var result = SqlQueryFormatter.Format("select id, name from users where id = 1");

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void Format_KeywordsAreUppercased()
    {
        var result = SqlQueryFormatter.Format("select id from users where active = 1 and role = 'admin'");

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
        Assert.Contains("AND", result);
    }

    [Fact]
    public void Format_InsertInto_CompoundKeyword()
    {
        var result = SqlQueryFormatter.Format("insert into users (id, name) values (1, 'test')");

        Assert.Contains("INSERT INTO", result);
        Assert.Contains("VALUES", result);
    }

    [Fact]
    public void Format_DeleteFrom_CompoundKeyword()
    {
        var result = SqlQueryFormatter.Format("delete from users where id = 1");

        Assert.Contains("DELETE FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void Format_OrderBy_CompoundKeyword()
    {
        var result = SqlQueryFormatter.Format("select id from users order by id desc");

        Assert.Contains("ORDER BY", result);
        Assert.Contains("DESC", result);
    }

    [Fact]
    public void Format_GroupBy_CompoundKeyword()
    {
        var result = SqlQueryFormatter.Format("select count(*) from users group by role");

        Assert.Contains("GROUP BY", result);
    }

    [Fact]
    public void Format_Update_WithSet()
    {
        var result = SqlQueryFormatter.Format("update users set name = 'test' where id = 1");

        Assert.Contains("UPDATE", result);
        Assert.Contains("SET", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void Format_PreservesStringLiterals()
    {
        var result = SqlQueryFormatter.Format("select * from users where name = 'John Doe'");

        Assert.Contains("'John Doe'", result);
    }

    [Fact]
    public void Format_HandlesLineComments()
    {
        var result = SqlQueryFormatter.Format("select id -- this is a comment\nfrom users");

        Assert.Contains("-- this is a comment", result);
    }

    [Fact]
    public void Format_HandlesBlockComments()
    {
        var result = SqlQueryFormatter.Format("select /* comment */ id from users");

        Assert.Contains("/* comment */", result);
    }

    [Fact]
    public void Format_HandlesParentheses()
    {
        var result = SqlQueryFormatter.Format("select count(*) from users");

        Assert.Contains("count(", result);
    }

    [Fact]
    public void Format_HandlesSemicolon()
    {
        var result = SqlQueryFormatter.Format("select 1;");

        Assert.Contains(";", result);
    }

    [Fact]
    public void Format_HandlesOperators()
    {
        var result = SqlQueryFormatter.Format("select * from users where age >= 18 and age <= 65");

        Assert.Contains(">=", result);
        Assert.Contains("<=", result);
    }

    [Fact]
    public void Format_HandlesJoins()
    {
        var result = SqlQueryFormatter.Format("select u.id from users u left join orders o on u.id = o.user_id");

        Assert.Contains("LEFT JOIN", result);
        Assert.Contains("ON", result);
    }

    [Fact]
    public void Format_ComplexQuery_DoesNotThrow()
    {
        var sql = @"SELECT c.id, c.name, COUNT(o.id) AS order_count
                    FROM customers c
                    LEFT JOIN orders o ON c.id = o.customer_id
                    WHERE c.status = 'active'
                      AND o.created_at >= '2024-01-01'
                    GROUP BY c.id, c.name
                    ORDER BY order_count DESC
                    LIMIT 10;";

        var result = SqlQueryFormatter.Format(sql);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("SELECT", result);
        Assert.Contains("LEFT JOIN", result);
        Assert.Contains("GROUP BY", result);
        Assert.Contains("ORDER BY", result);
        Assert.Contains("LIMIT", result);
    }

    [Fact]
    public void Format_UnionAll_CompoundKeyword()
    {
        var result = SqlQueryFormatter.Format("select 1 union all select 2");

        Assert.Contains("UNION ALL", result);
    }

    [Fact]
    public void Format_BacktickQuotedIdentifiers_Preserved()
    {
        var result = SqlQueryFormatter.Format("select `user name` from `my table`");

        Assert.Contains("`user name`", result);
        Assert.Contains("`my table`", result);
    }
}
