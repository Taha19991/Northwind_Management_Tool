using System.Data.Common;

namespace Northwind.Core.Interfaces;

public interface IReadOnlyDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken ct = default);
}
