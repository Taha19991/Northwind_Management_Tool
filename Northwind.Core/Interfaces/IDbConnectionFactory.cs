using System.Data.Common;

namespace Northwind.Core.Interfaces;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken ct = default);
}
