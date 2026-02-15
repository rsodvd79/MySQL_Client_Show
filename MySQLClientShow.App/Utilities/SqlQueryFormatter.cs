using System.Text;

namespace MySQLClientShow.App.Utilities;

public static class SqlQueryFormatter
{
    private const int IndentSize = 4;

    private static readonly HashSet<string> ClauseKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP BY",
        "ORDER BY",
        "HAVING",
        "LIMIT",
        "OFFSET",
        "INSERT INTO",
        "UPDATE",
        "DELETE FROM",
        "VALUES",
        "SET",
        "JOIN",
        "LEFT JOIN",
        "RIGHT JOIN",
        "INNER JOIN",
        "FULL JOIN",
        "CROSS JOIN",
        "ON",
        "UNION",
        "UNION ALL"
    };

    private static readonly HashSet<string> UpperKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP",
        "BY",
        "ORDER",
        "HAVING",
        "LIMIT",
        "OFFSET",
        "INSERT",
        "INTO",
        "UPDATE",
        "DELETE",
        "VALUES",
        "SET",
        "JOIN",
        "LEFT",
        "RIGHT",
        "INNER",
        "FULL",
        "CROSS",
        "ON",
        "UNION",
        "ALL",
        "AS",
        "AND",
        "OR",
        "IN",
        "IS",
        "NOT",
        "NULL",
        "LIKE",
        "BETWEEN",
        "EXISTS",
        "DISTINCT",
        "CASE",
        "WHEN",
        "THEN",
        "ELSE",
        "END",
        "ASC",
        "DESC"
    };

    private static readonly string[][] CompoundKeywords =
    {
        new[] { "INSERT", "INTO" },
        new[] { "DELETE", "FROM" },
        new[] { "GROUP", "BY" },
        new[] { "ORDER", "BY" },
        new[] { "LEFT", "JOIN" },
        new[] { "RIGHT", "JOIN" },
        new[] { "INNER", "JOIN" },
        new[] { "FULL", "JOIN" },
        new[] { "CROSS", "JOIN" },
        new[] { "UNION", "ALL" }
    };

    private static readonly HashSet<string> OperatorTokens = new(StringComparer.Ordinal)
    {
        "=",
        "<",
        ">",
        "<=",
        ">=",
        "<>",
        "!=",
        "+",
        "-",
        "*",
        "/",
        "%"
    };

    public static string Format(string? sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
        {
            return string.Empty;
        }

        var tokens = Tokenize(sqlText);
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(sqlText.Length + 64);
        var indentLevel = 0;
        var isLineStart = true;
        var currentClause = ClauseKind.None;
        var previousWord = string.Empty;
        var inlineParentheses = new Stack<bool>();

        for (var i = 0; i < tokens.Count; i++)
        {
            if (TryReadCompoundKeyword(tokens, i, out var compoundKeyword, out var consumed))
            {
                if (!isLineStart)
                {
                    NewLine(sb, ref isLineStart);
                }

                WriteToken(sb, compoundKeyword, indentLevel, ref isLineStart);
                currentClause = ResolveClause(compoundKeyword);
                previousWord = compoundKeyword[(compoundKeyword.LastIndexOf(' ') + 1)..];
                i += consumed - 1;
                continue;
            }

            var token = tokens[i];
            if (IsCommentToken(token))
            {
                if (!isLineStart)
                {
                    NewLine(sb, ref isLineStart);
                }

                WriteToken(sb, token.Trim(), indentLevel, ref isLineStart);
                NewLine(sb, ref isLineStart);
                previousWord = string.Empty;
                continue;
            }

            if (token == "(")
            {
                var inlineParenthesis = IsInlineParenthesis(previousWord);
                inlineParentheses.Push(inlineParenthesis);

                TrimTrailingSpaces(sb);
                if (inlineParenthesis)
                {
                    sb.Append('(');
                    sb.Append(' ');
                    isLineStart = false;
                }
                else
                {
                    sb.Append(" (");
                    indentLevel++;
                    NewLine(sb, ref isLineStart);
                }

                previousWord = string.Empty;
                continue;
            }

            if (token == ")")
            {
                var inlineParenthesis = inlineParentheses.Count > 0 && inlineParentheses.Pop();
                TrimTrailingSpaces(sb);

                if (inlineParenthesis)
                {
                    sb.Append(')');
                    sb.Append(' ');
                    isLineStart = false;
                }
                else
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                    if (!isLineStart)
                    {
                        NewLine(sb, ref isLineStart);
                    }

                    WriteToken(sb, ")", indentLevel, ref isLineStart);
                }

                previousWord = string.Empty;
                continue;
            }

            if (token == ",")
            {
                TrimTrailingSpaces(sb);
                sb.Append(',');

                if (ShouldBreakAfterComma(currentClause))
                {
                    NewLine(sb, ref isLineStart);
                }
                else
                {
                    sb.Append(' ');
                    isLineStart = false;
                }

                previousWord = string.Empty;
                continue;
            }

            if (token == ";")
            {
                TrimTrailingSpaces(sb);
                sb.Append(';');
                NewLine(sb, ref isLineStart);
                currentClause = ClauseKind.None;
                previousWord = string.Empty;
                continue;
            }

            if (IsWordToken(token))
            {
                var upperToken = token.ToUpperInvariant();

                if (ClauseKeywords.Contains(upperToken))
                {
                    if (!isLineStart)
                    {
                        NewLine(sb, ref isLineStart);
                    }

                    WriteToken(sb, upperToken, indentLevel, ref isLineStart);
                    currentClause = ResolveClause(upperToken);
                    previousWord = upperToken;
                    continue;
                }

                if ((upperToken == "AND" || upperToken == "OR") && ShouldBreakForCondition(currentClause))
                {
                    if (!isLineStart)
                    {
                        NewLine(sb, ref isLineStart);
                    }

                    WriteToken(sb, upperToken, indentLevel + 1, ref isLineStart);
                    previousWord = upperToken;
                    continue;
                }

                var formattedToken = UpperKeywords.Contains(upperToken) ? upperToken : token;
                WriteToken(sb, formattedToken, indentLevel, ref isLineStart);
                previousWord = upperToken;
                continue;
            }

            if (OperatorTokens.Contains(token))
            {
                WriteToken(sb, token, indentLevel, ref isLineStart);
                previousWord = string.Empty;
                continue;
            }

            WriteToken(sb, token, indentLevel, ref isLineStart);
            previousWord = string.Empty;
        }

        TrimTrailingSpaces(sb);
        return sb.ToString().Trim();
    }

    private static bool TryReadCompoundKeyword(IReadOnlyList<string> tokens, int startIndex, out string keyword, out int consumed)
    {
        foreach (var parts in CompoundKeywords)
        {
            if (startIndex + parts.Length > tokens.Count)
            {
                continue;
            }

            var isMatch = true;
            for (var i = 0; i < parts.Length; i++)
            {
                if (!IsWordToken(tokens[startIndex + i]) ||
                    !string.Equals(tokens[startIndex + i], parts[i], StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = false;
                    break;
                }
            }

            if (!isMatch)
            {
                continue;
            }

            keyword = string.Join(' ', parts);
            consumed = parts.Length;
            return true;
        }

        keyword = string.Empty;
        consumed = 0;
        return false;
    }

    private static ClauseKind ResolveClause(string keyword)
    {
        return keyword.ToUpperInvariant() switch
        {
            "SELECT" => ClauseKind.Select,
            "FROM" => ClauseKind.From,
            "WHERE" => ClauseKind.Where,
            "HAVING" => ClauseKind.Having,
            "GROUP BY" => ClauseKind.GroupBy,
            "ORDER BY" => ClauseKind.OrderBy,
            "SET" => ClauseKind.Set,
            "VALUES" => ClauseKind.Values,
            "JOIN" or "LEFT JOIN" or "RIGHT JOIN" or "INNER JOIN" or "FULL JOIN" or "CROSS JOIN" => ClauseKind.Join,
            "ON" => ClauseKind.On,
            _ => ClauseKind.Other
        };
    }

    private static bool ShouldBreakAfterComma(ClauseKind clause)
    {
        return clause is ClauseKind.Select or ClauseKind.GroupBy or ClauseKind.OrderBy or ClauseKind.Set;
    }

    private static bool ShouldBreakForCondition(ClauseKind clause)
    {
        return clause is ClauseKind.Where or ClauseKind.Having or ClauseKind.On;
    }

    private static bool IsInlineParenthesis(string previousWord)
    {
        if (string.IsNullOrEmpty(previousWord))
        {
            return false;
        }

        return previousWord.ToUpperInvariant() switch
        {
            "IN" => false,
            "EXISTS" => false,
            "FROM" => false,
            "JOIN" => false,
            "ON" => false,
            "WHERE" => false,
            "SELECT" => false,
            "VALUES" => false,
            "SET" => false,
            "AND" => false,
            "OR" => false,
            _ => true
        };
    }

    private static bool IsCommentToken(string token)
    {
        return token.StartsWith("--", StringComparison.Ordinal) ||
               token.StartsWith("/*", StringComparison.Ordinal);
    }

    private static bool IsWordToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (token is "," or "(" or ")" or ";")
        {
            return false;
        }

        if (OperatorTokens.Contains(token))
        {
            return false;
        }

        if (IsCommentToken(token))
        {
            return false;
        }

        return token[0] is not '\'' and not '"' and not '`';
    }

    private static List<string> Tokenize(string sqlText)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < sqlText.Length; i++)
        {
            var ch = sqlText[i];

            if (char.IsWhiteSpace(ch))
            {
                FlushToken();
                continue;
            }

            if (ch == '\'' || ch == '"' || ch == '`')
            {
                FlushToken();
                tokens.Add(ReadQuotedToken(sqlText, ref i, ch));
                continue;
            }

            if (ch == '-' && i + 1 < sqlText.Length && sqlText[i + 1] == '-')
            {
                FlushToken();
                tokens.Add(ReadLineComment(sqlText, ref i));
                continue;
            }

            if (ch == '/' && i + 1 < sqlText.Length && sqlText[i + 1] == '*')
            {
                FlushToken();
                tokens.Add(ReadBlockComment(sqlText, ref i));
                continue;
            }

            if (ch is '(' or ')' or ',' or ';')
            {
                FlushToken();
                tokens.Add(ch.ToString());
                continue;
            }

            if (ch is '=' or '<' or '>' or '!' or '+' or '-' or '*' or '/' or '%')
            {
                FlushToken();
                tokens.Add(ReadOperator(sqlText, ref i));
                continue;
            }

            current.Append(ch);
        }

        FlushToken();
        return tokens;

        void FlushToken()
        {
            if (current.Length == 0)
            {
                return;
            }

            tokens.Add(current.ToString());
            current.Clear();
        }
    }

    private static string ReadQuotedToken(string sqlText, ref int index, char quoteChar)
    {
        var token = new StringBuilder();
        token.Append(sqlText[index]);

        for (index++; index < sqlText.Length; index++)
        {
            var current = sqlText[index];
            token.Append(current);

            if (current == '\\' && index + 1 < sqlText.Length)
            {
                token.Append(sqlText[index + 1]);
                index++;
                continue;
            }

            if (current != quoteChar)
            {
                continue;
            }

            if (index + 1 < sqlText.Length && sqlText[index + 1] == quoteChar)
            {
                token.Append(sqlText[index + 1]);
                index++;
                continue;
            }

            break;
        }

        return token.ToString();
    }

    private static string ReadLineComment(string sqlText, ref int index)
    {
        var token = new StringBuilder();
        token.Append(sqlText[index]);
        token.Append(sqlText[index + 1]);
        index += 2;

        while (index < sqlText.Length)
        {
            var current = sqlText[index];
            if (current is '\r' or '\n')
            {
                index--;
                break;
            }

            token.Append(current);
            index++;
        }

        return token.ToString();
    }

    private static string ReadBlockComment(string sqlText, ref int index)
    {
        var token = new StringBuilder();
        token.Append(sqlText[index]);
        token.Append(sqlText[index + 1]);
        index += 2;

        while (index < sqlText.Length)
        {
            token.Append(sqlText[index]);

            if (sqlText[index] == '*' &&
                index + 1 < sqlText.Length &&
                sqlText[index + 1] == '/')
            {
                token.Append(sqlText[index + 1]);
                index++;
                break;
            }

            index++;
        }

        return token.ToString();
    }

    private static string ReadOperator(string sqlText, ref int index)
    {
        var current = sqlText[index];

        if (index + 1 >= sqlText.Length)
        {
            return current.ToString();
        }

        var next = sqlText[index + 1];
        if ((current == '<' && (next == '=' || next == '>')) ||
            (current == '>' && next == '=') ||
            (current == '!' && next == '='))
        {
            index++;
            return string.Concat(current, next);
        }

        return current.ToString();
    }

    private static void WriteToken(StringBuilder target, string token, int indentLevel, ref bool isLineStart)
    {
        if (isLineStart && indentLevel > 0)
        {
            target.Append(' ', indentLevel * IndentSize);
        }

        target.Append(token);
        target.Append(' ');
        isLineStart = false;
    }

    private static void NewLine(StringBuilder target, ref bool isLineStart)
    {
        TrimTrailingSpaces(target);
        if (target.Length == 0 || target[^1] == '\n')
        {
            isLineStart = true;
            return;
        }

        target.AppendLine();
        isLineStart = true;
    }

    private static void TrimTrailingSpaces(StringBuilder target)
    {
        while (target.Length > 0 && (target[^1] == ' ' || target[^1] == '\t'))
        {
            target.Length--;
        }
    }

    private enum ClauseKind
    {
        None,
        Select,
        From,
        Where,
        GroupBy,
        OrderBy,
        Having,
        Set,
        Values,
        Join,
        On,
        Other
    }
}
