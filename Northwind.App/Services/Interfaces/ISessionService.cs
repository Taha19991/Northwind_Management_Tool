using Northwind.Core.Enums;
using Northwind.Core.Models;

namespace Northwind.App.Services.Interfaces;

public interface ISessionService
{
    AppUser? CurrentUser { get; }
    bool IsAuthenticated { get; }
    event EventHandler? SessionChanged;

    void SetUser(AppUser user);
    bool HasRole(UserRole role);
    bool HasAnyRole(params UserRole[] roles);
    void Logout();
}
