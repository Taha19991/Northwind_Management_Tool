namespace Northwind.App.Services.Interfaces;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password, CancellationToken ct = default);
}
