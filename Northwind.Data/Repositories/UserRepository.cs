using Dapper;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        const string sql = """
            SELECT user_id, username, password_hash, role, employee_id, is_active
            FROM app_users
            WHERE username = @Username AND is_active = TRUE;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { Username = username }, cancellationToken: ct);
        return await connection.QuerySingleOrDefaultAsync<AppUser>(command).ConfigureAwait(false);
    }
}
