using Dapper;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class CustomerRepository(IDbConnectionFactory connectionFactory) : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Customer>> SearchAsync(string? text, CancellationToken ct = default)
    {
        const string sqlAll = """
            SELECT customer_id, company_name, contact_name, contact_title,
                   address, city, region, postal_code, country, phone, fax
            FROM customers
            ORDER BY company_name;
            """;

        const string sqlFiltered = """
            SELECT customer_id, company_name, contact_name, contact_title,
                   address, city, region, postal_code, country, phone, fax
            FROM customers
            WHERE customer_id ILIKE @Pattern
               OR company_name ILIKE @Pattern
               OR contact_name ILIKE @Pattern
               OR city ILIKE @Pattern
               OR country ILIKE @Pattern
            ORDER BY company_name;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var trimmed = text?.Trim();
            var command = string.IsNullOrEmpty(trimmed)
                ? new CommandDefinition(sqlAll, cancellationToken: ct)
                : new CommandDefinition(sqlFiltered, new { Pattern = $"%{trimmed}%" }, cancellationToken: ct);

            var rows = await connection.QueryAsync<Customer>(command).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<Customer?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT customer_id, company_name, contact_name, contact_title,
                   address, city, region, postal_code, country, phone, fax
            FROM customers
            WHERE customer_id = @Id;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var command = new CommandDefinition(sql, new { Id = id.Trim().ToUpperInvariant() }, cancellationToken: ct);
            return await connection.QuerySingleOrDefaultAsync<Customer>(command).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task InsertAsync(Customer c, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO customers (
                customer_id, company_name, contact_name, contact_title,
                address, city, region, postal_code, country, phone, fax)
            VALUES (
                @CustomerId, @CompanyName, @ContactName, @ContactTitle,
                @Address, @City, @Region, @PostalCode, @Country, @Phone, @Fax);
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var parameters = new
            {
                CustomerId = c.CustomerId.Trim().ToUpperInvariant(),
                CompanyName = c.CompanyName.Trim(),
                ContactName = NullIfBlank(c.ContactName),
                ContactTitle = NullIfBlank(c.ContactTitle),
                Address = NullIfBlank(c.Address),
                City = NullIfBlank(c.City),
                Region = NullIfBlank(c.Region),
                PostalCode = NullIfBlank(c.PostalCode),
                Country = NullIfBlank(c.Country),
                Phone = NullIfBlank(c.Phone),
                Fax = NullIfBlank(c.Fax)
            };

            await connection.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task UpdateAsync(Customer c, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE customers
            SET company_name  = @CompanyName,
                contact_name  = @ContactName,
                contact_title = @ContactTitle,
                address       = @Address,
                city          = @City,
                region        = @Region,
                postal_code   = @PostalCode,
                country       = @Country,
                phone         = @Phone,
                fax           = @Fax
            WHERE customer_id = @CustomerId;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var parameters = new
            {
                CustomerId = c.CustomerId.Trim().ToUpperInvariant(),
                CompanyName = c.CompanyName.Trim(),
                ContactName = NullIfBlank(c.ContactName),
                ContactTitle = NullIfBlank(c.ContactTitle),
                Address = NullIfBlank(c.Address),
                City = NullIfBlank(c.City),
                Region = NullIfBlank(c.Region),
                PostalCode = NullIfBlank(c.PostalCode),
                Country = NullIfBlank(c.Country),
                Phone = NullIfBlank(c.Phone),
                Fax = NullIfBlank(c.Fax)
            };

            await connection.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(string customerId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT o.order_id, o.customer_id, o.employee_id, o.order_date,
                   o.required_date, o.shipped_date, o.ship_via, o.freight,
                   s.company_name AS shipper_name,
                   COALESCE(ROUND(SUM(v.net_amount), 2), 0) AS net_total
            FROM orders o
            LEFT JOIN shippers s ON s.shipper_id = o.ship_via
            LEFT JOIN v_order_lines v ON v.order_id = o.order_id
            WHERE o.customer_id = @CustomerId
            GROUP BY o.order_id, o.customer_id, o.employee_id, o.order_date,
                     o.required_date, o.shipped_date, o.ship_via, o.freight, s.company_name
            ORDER BY o.order_date DESC, o.order_id DESC;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Order>(
                new CommandDefinition(sql, new { CustomerId = customerId.Trim().ToUpperInvariant() }, cancellationToken: ct))
                .ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
