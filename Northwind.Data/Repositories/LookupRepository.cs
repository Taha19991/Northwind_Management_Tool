using Dapper;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT category_id, category_name, description
            FROM categories
            ORDER BY category_name;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Category>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT supplier_id, company_name, contact_name, contact_title,
                   address, city, region, postal_code, country, phone, fax, homepage
            FROM suppliers
            ORDER BY company_name;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Supplier>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<IReadOnlyList<Shipper>> GetShippersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT shipper_id, company_name, phone
            FROM shippers
            ORDER BY company_name;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Shipper>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT employee_id, last_name, first_name, title, title_of_courtesy,
                   birth_date, hire_date, address, city, region, postal_code, country,
                   home_phone, extension, notes, reports_to, photo_path
            FROM employees
            ORDER BY first_name, last_name;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Employee>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }
}
