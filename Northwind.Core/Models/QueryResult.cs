using System.Data;

namespace Northwind.Core.Models;

public sealed class QueryResult
{
    public DataTable Table { get; init; } = new();
    public int RowCount { get; init; }
    public bool Truncated { get; init; }
    public bool TotalCountUnknown { get; init; }
    public long ElapsedMs { get; init; }
}
