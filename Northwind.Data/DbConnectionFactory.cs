using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Northwind.Core.Exceptions;
using Northwind.Core.Interfaces;
using Npgsql;

namespace Northwind.Data;

public sealed class DbConnectionFactory : IDbConnectionFactory, IAsyncDisposable, IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public DbConnectionFactory(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Northwind");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new UserFriendlyException(
                "Database connection string ('ConnectionStrings:Northwind') is not configured.");
        }

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task<DbConnection> OpenAsync(CancellationToken ct = default)
    {
        return await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
    }

    public void Dispose() => _dataSource.Dispose();

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
