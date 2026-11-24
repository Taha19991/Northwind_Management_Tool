using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.Data;

public sealed class SqlKeywords : ISqlCompletionSource
{
    public IReadOnlyList<SqlKeywordItem> Keywords { get; } =
    [
        // Clauses & operators
        new("SELECT", SqlKeywordKind.Keyword, "Query columns or expressions"),
        new("FROM", SqlKeywordKind.Keyword, "Specify source table or view"),
        new("WHERE", SqlKeywordKind.Keyword, "Filter rows by condition"),
        new("JOIN", SqlKeywordKind.Keyword, "Combine rows from two tables"),
        new("LEFT JOIN", SqlKeywordKind.Keyword, "Left outer join"),
        new("INNER JOIN", SqlKeywordKind.Keyword, "Inner join"),
        new("RIGHT JOIN", SqlKeywordKind.Keyword, "Right outer join"),
        new("FULL JOIN", SqlKeywordKind.Keyword, "Full outer join"),
        new("CROSS JOIN", SqlKeywordKind.Keyword, "Cartesian product join"),
        new("ON", SqlKeywordKind.Keyword, "Join condition"),
        new("GROUP BY", SqlKeywordKind.Keyword, "Group rows for aggregation"),
        new("ORDER BY", SqlKeywordKind.Keyword, "Sort query results"),
        new("HAVING", SqlKeywordKind.Keyword, "Filter grouped rows"),
        new("LIMIT", SqlKeywordKind.Keyword, "Maximum number of rows to return"),
        new("OFFSET", SqlKeywordKind.Keyword, "Number of rows to skip"),
        new("DISTINCT", SqlKeywordKind.Keyword, "Eliminate duplicate rows"),
        new("AS", SqlKeywordKind.Keyword, "Column or table alias"),
        new("AND", SqlKeywordKind.Keyword, "Logical AND operator"),
        new("OR", SqlKeywordKind.Keyword, "Logical OR operator"),
        new("NOT", SqlKeywordKind.Keyword, "Logical negation"),
        new("IN", SqlKeywordKind.Keyword, "Match value against a list or subquery"),
        new("IS NULL", SqlKeywordKind.Keyword, "Test for NULL value"),
        new("IS NOT NULL", SqlKeywordKind.Keyword, "Test for non-NULL value"),
        new("BETWEEN", SqlKeywordKind.Keyword, "Inclusive range predicate"),
        new("LIKE", SqlKeywordKind.Keyword, "Case-sensitive pattern match"),
        new("ILIKE", SqlKeywordKind.Keyword, "Case-insensitive pattern match (PostgreSQL)"),
        new("CASE", SqlKeywordKind.Keyword, "Conditional expression start"),
        new("WHEN", SqlKeywordKind.Keyword, "Branch condition in CASE"),
        new("THEN", SqlKeywordKind.Keyword, "Result expression in CASE"),
        new("ELSE", SqlKeywordKind.Keyword, "Fallback expression in CASE"),
        new("END", SqlKeywordKind.Keyword, "End of CASE expression"),
        new("FILTER", SqlKeywordKind.Keyword, "Aggregate filter clause (FILTER (WHERE ...))"),
        new("WITH", SqlKeywordKind.Keyword, "Common Table Expression (CTE)"),
        new("UNION", SqlKeywordKind.Keyword, "Combine result sets (deduplicated)"),
        new("UNION ALL", SqlKeywordKind.Keyword, "Combine result sets (keep duplicates)"),
        new("EXISTS", SqlKeywordKind.Keyword, "Test whether a subquery returns any row"),
        new("ASC", SqlKeywordKind.Keyword, "Ascending sort order"),
        new("DESC", SqlKeywordKind.Keyword, "Descending sort order"),
        new("NULLS FIRST", SqlKeywordKind.Keyword, "Place NULLs first when sorting"),
        new("NULLS LAST", SqlKeywordKind.Keyword, "Place NULLs last when sorting"),
        new("CAST", SqlKeywordKind.Keyword, "Convert value to another data type"),
        new("INTERVAL", SqlKeywordKind.Keyword, "Date/time interval literal"),
        new("TRUE", SqlKeywordKind.Keyword, "Boolean true literal"),
        new("FALSE", SqlKeywordKind.Keyword, "Boolean false literal"),
        new("NULL", SqlKeywordKind.Keyword, "SQL NULL literal"),

        // Functions
        new("COUNT", SqlKeywordKind.Function, "COUNT(expr) — number of rows"),
        new("SUM", SqlKeywordKind.Function, "SUM(expr) — total sum"),
        new("AVG", SqlKeywordKind.Function, "AVG(expr) — average value"),
        new("MIN", SqlKeywordKind.Function, "MIN(expr) — minimum value"),
        new("MAX", SqlKeywordKind.Function, "MAX(expr) — maximum value"),
        new("ROUND", SqlKeywordKind.Function, "ROUND(value, scale) — round numeric value"),
        new("COALESCE", SqlKeywordKind.Function, "COALESCE(a, b, ...) — first non-NULL value"),
        new("NULLIF", SqlKeywordKind.Function, "NULLIF(a, b) — return NULL if a = b"),
        new("EXTRACT", SqlKeywordKind.Function, "EXTRACT(field FROM source) — date/time part"),
        new("DATE_TRUNC", SqlKeywordKind.Function, "DATE_TRUNC(precision, source) — truncate timestamp"),
        new("NOW", SqlKeywordKind.Function, "NOW() — current transaction timestamp"),
        new("CURRENT_DATE", SqlKeywordKind.Function, "CURRENT_DATE — current date"),
        new("LOWER", SqlKeywordKind.Function, "LOWER(text) — lowercase text"),
        new("UPPER", SqlKeywordKind.Function, "UPPER(text) — uppercase text"),
        new("LENGTH", SqlKeywordKind.Function, "LENGTH(text) — character length"),
        new("TO_CHAR", SqlKeywordKind.Function, "TO_CHAR(value, format) — format date or number")
    ];

    public IReadOnlySet<string> TableContextTriggers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FROM",
        "JOIN",
        "UPDATE",
        "INTO"
    };

    public IReadOnlySet<string> ReservedClauseWords { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "CROSS", "NATURAL",
        "ON", "USING", "GROUP", "ORDER", "BY", "HAVING", "LIMIT", "OFFSET", "UNION", "INTERSECT", "EXCEPT",
        "AS", "AND", "OR", "NOT", "IN", "IS", "NULL", "SET", "VALUES", "INTO", "WHEN", "THEN", "ELSE", "END",
        "ASC", "DESC", "DISTINCT", "FILTER", "WITH", "CASE", "BETWEEN", "LIKE", "ILIKE", "EXISTS"
    };
}
