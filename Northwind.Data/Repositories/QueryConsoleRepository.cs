using System.Data;
using System.Diagnostics;
using Dapper;
using Northwind.Core.Exceptions;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class QueryConsoleRepository(IReadOnlyDbConnectionFactory readOnlyConnectionFactory)
    : IQueryConsoleRepository
{
    private const int DefaultCommandTimeoutSeconds = 20;
    private const int MaxCountedRows = 100_000;

    private readonly IReadOnlyDbConnectionFactory _readOnlyConnectionFactory = readOnlyConnectionFactory;

    public async Task<IReadOnlyList<SchemaObject>> GetSchemaAsync(CancellationToken ct = default)
    {
        const string objectsSql = """
            SELECT table_name, table_type
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type IN ('BASE TABLE', 'VIEW')
            ORDER BY table_name;
            """;

        const string columnsSql = """
            SELECT table_name, column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
            ORDER BY table_name, ordinal_position;
            """;

        const string primaryKeysSql = """
            SELECT kcu.table_name, kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON kcu.constraint_name = tc.constraint_name
             AND kcu.table_schema = tc.table_schema
             AND kcu.table_name = tc.table_name
            WHERE tc.table_schema = 'public'
              AND tc.constraint_type = 'PRIMARY KEY';
            """;

        try
        {
            await using var conn = await _readOnlyConnectionFactory.OpenAsync(ct);

            var objectRows = (await conn.QueryAsync<SchemaObjectRow>(
                new CommandDefinition(objectsSql, cancellationToken: ct))).ToList();
            var columnRows = (await conn.QueryAsync<SchemaColumnRow>(
                new CommandDefinition(columnsSql, cancellationToken: ct))).ToList();
            var pkRows = (await conn.QueryAsync<PrimaryKeyRow>(
                new CommandDefinition(primaryKeysSql, cancellationToken: ct))).ToList();

            var pkLookup = new HashSet<(string Table, string Column)>(
                pkRows.Select(r => (r.TableName, r.ColumnName)));

            var objectMap = new Dictionary<string, SchemaObject>(StringComparer.OrdinalIgnoreCase);
            var result = new List<SchemaObject>(objectRows.Count);

            foreach (var obj in objectRows)
            {
                var item = new SchemaObject
                {
                    Name = obj.TableName,
                    Kind = string.Equals(obj.TableType, "VIEW", StringComparison.OrdinalIgnoreCase)
                        ? SchemaObjectKind.View
                        : SchemaObjectKind.Table
                };
                objectMap[obj.TableName] = item;
                result.Add(item);
            }

            foreach (var col in columnRows)
            {
                if (!objectMap.TryGetValue(col.TableName, out var owner))
                {
                    continue;
                }

                owner.Columns.Add(new SchemaColumn
                {
                    Name = col.ColumnName,
                    DataType = col.DataType,
                    IsNullable = string.Equals(col.IsNullable, "YES", StringComparison.OrdinalIgnoreCase),
                    IsPrimaryKey = pkLookup.Contains((col.TableName, col.ColumnName))
                });
            }

            return result;
        }
        catch (PostgresException ex)
        {
            throw new QueryConsoleException(ex.MessageText, ex.SqlState, ex.Position, ex);
        }
    }

    public async Task<QueryResult> ExecuteAsync(string sql, int maxRows = 1000, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new QueryConsoleException("Enter a SQL query to run.");
        }

        if (maxRows <= 0)
        {
            maxRows = 1000;
        }

        var stopwatch = Stopwatch.StartNew();

        await using var conn = await _readOnlyConnectionFactory.OpenAsync(ct);
        var transactionStarted = false;

        try
        {
            await using (var beginCmd = conn.CreateCommand())
            {
                beginCmd.CommandText = "BEGIN TRANSACTION READ ONLY;";
                beginCmd.CommandTimeout = DefaultCommandTimeoutSeconds;
                await beginCmd.ExecuteNonQueryAsync(ct);
                transactionStarted = true;
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = DefaultCommandTimeoutSeconds;

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var table = BuildSchemaTable(reader);
            var fieldCount = reader.FieldCount;
            var totalRows = 0;
            var truncated = false;
            var totalCountUnknown = false;

            while (await reader.ReadAsync(ct))
            {
                totalRows++;

                if (totalRows <= maxRows)
                {
                    var values = new object[fieldCount];
                    for (var i = 0; i < fieldCount; i++)
                    {
                        if (reader.IsDBNull(i))
                        {
                            values[i] = DBNull.Value;
                        }
                        else
                        {
                            var raw = reader.GetValue(i);
                            values[i] = raw is byte[] bytes
                                ? $"<binary {bytes.Length} bytes>"
                                : raw;
                        }
                    }

                    table.Rows.Add(values);
                }
                else
                {
                    truncated = true;
                    if (totalRows > MaxCountedRows)
                    {
                        totalCountUnknown = true;
                        break;
                    }
                }
            }

            stopwatch.Stop();

            return new QueryResult
            {
                Table = table,
                RowCount = totalCountUnknown ? MaxCountedRows : totalRows,
                Truncated = truncated,
                TotalCountUnknown = totalCountUnknown,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (PostgresException ex) when (ct.IsCancellationRequested && ex.SqlState == PostgresErrorCodes.QueryCanceled)
        {
            throw new OperationCanceledException("Query cancelled by user.", ex, ct);
        }
        catch (PostgresException ex)
        {
            throw new QueryConsoleException(ex.MessageText, ex.SqlState, ex.Position, ex);
        }
        finally
        {
            if (transactionStarted && conn.State == ConnectionState.Open)
            {
                try
                {
                    await using var rollbackCmd = conn.CreateCommand();
                    rollbackCmd.CommandText = "ROLLBACK;";
                    rollbackCmd.CommandTimeout = 5;
                    await rollbackCmd.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch
                {
                    // Connection is about to be closed/discarded; ignore rollback cleanup failures.
                }
            }
        }
    }

    private static DataTable BuildSchemaTable(System.Data.Common.DbDataReader reader)
    {
        var table = new DataTable();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var baseName = reader.GetName(i);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = $"column_{i + 1}";
            }

            var columnName = baseName;
            var suffix = 2;
            while (table.Columns.Contains(columnName))
            {
                columnName = $"{baseName}_{suffix++}";
            }

            var fieldType = reader.GetFieldType(i);
            var columnType = fieldType == typeof(byte[]) ? typeof(string) : fieldType;
            var column = new DataColumn(columnName, columnType)
            {
                AllowDBNull = true
            };
            table.Columns.Add(column);
        }

        return table;
    }

    private sealed class SchemaObjectRow
    {
        public string TableName { get; set; } = string.Empty;
        public string TableType { get; set; } = string.Empty;
    }

    private sealed class SchemaColumnRow
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string IsNullable { get; set; } = string.Empty;
    }

    private sealed class PrimaryKeyRow
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
    }
}
