using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Models;

namespace Northwind.App.Services;

public sealed class SessionService : ISessionService
{
    public AppUser? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public event EventHandler? SessionChanged;

    public void SetUser(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        CurrentUser = user;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasRole(UserRole role) =>
        CurrentUser is not null && CurrentUser.Role == role;

    public bool HasAnyRole(params UserRole[] roles)
    {
        if (CurrentUser is null)
        {
            return false;
        }

        foreach (var role in roles)
        {
            if (CurrentUser.Role == role)
            {
                return true;
            }
        }

        return false;
    }

    public void Logout()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
