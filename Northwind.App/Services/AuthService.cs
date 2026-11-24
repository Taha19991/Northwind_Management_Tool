using Northwind.App.Services.Interfaces;
using Northwind.Core.Interfaces;

namespace Northwind.App.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;

    public AuthService(IUserRepository userRepository, ISessionService sessionService)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
    }

    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var normalizedUsername = username?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedUsername) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var user = await _userRepository.GetByUsernameAsync(normalizedUsername, ct);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        var isValid = await Task.Run(
            () => BCrypt.Net.BCrypt.Verify(password, user.PasswordHash),
            ct);

        if (!isValid)
        {
            return false;
        }

        _sessionService.SetUser(user);
        return true;
    }
}
