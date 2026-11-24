using Dapper;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class EmployeeRepository(IDbConnectionFactory connectionFactory) : IEmployeeRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT employee_id, last_name, first_name, title, title_of_courtesy,
                   birth_date, hire_date, address, city, region, postal_code, country,
                   home_phone, extension, notes, reports_to, photo_path
            FROM employees
            ORDER BY employee_id;
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

    public async Task<IReadOnlyList<Territory>> GetTerritoriesAsync(short employeeId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TRIM(t.territory_id) AS territory_id,
                   TRIM(t.territory_description) AS territory_description,
                   t.region_id,
                   TRIM(r.region_description) AS region_description
            FROM employee_territories et
            JOIN territories t ON t.territory_id = et.territory_id
            JOIN region r ON r.region_id = t.region_id
            WHERE et.employee_id = @EmployeeId
            ORDER BY t.territory_id;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Territory>(
                new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }
}
