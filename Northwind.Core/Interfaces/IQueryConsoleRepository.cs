using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface IQueryConsoleRepository
{
    Task<IReadOnlyList<SchemaObject>> GetSchemaAsync(CancellationToken ct = default);
    Task<QueryResult> ExecuteAsync(string sql, int maxRows = 1000, CancellationToken ct = default);
}
