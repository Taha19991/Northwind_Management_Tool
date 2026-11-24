using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
}
