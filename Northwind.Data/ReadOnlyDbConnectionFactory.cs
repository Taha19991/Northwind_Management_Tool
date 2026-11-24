using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Northwind.Core.Exceptions;
using Northwind.Core.Interfaces;
using Npgsql;

namespace Northwind.Data;

public sealed class ReadOnlyDbConnectionFactory : IReadOnlyDbConnectionFactory, IAsyncDisposable, IDisposable
{
    private readonly string? _connectionString;
    private readonly object _sync = new();
    private NpgsqlDataSource? _dataSource;

    public ReadOnlyDbConnectionFactory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("NorthwindReadOnly");
    }

    public async Task<DbConnection> OpenAsync(CancellationToken ct = default)
    {
        var dataSource = GetOrCreateDataSource();
        return await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
    }

    private NpgsqlDataSource GetOrCreateDataSource()
    {
        if (_dataSource is not null)
        {
            return _dataSource;
        }

        lock (_sync)
        {
            if (_dataSource is not null)
            {
                return _dataSource;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new UserFriendlyException(
                    "Read-only database connection string ('ConnectionStrings:NorthwindReadOnly') is not configured.");
            }

            _dataSource = NpgsqlDataSource.Create(_connectionString);
            return _dataSource;
        }
    }

    public void Dispose() => _dataSource?.Dispose();

    public ValueTask DisposeAsync() =>
        _dataSource is null ? ValueTask.CompletedTask : _dataSource.DisposeAsync();
}
