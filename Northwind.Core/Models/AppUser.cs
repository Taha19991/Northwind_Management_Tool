using Northwind.Core.Enums;

namespace Northwind.Core.Models;

public sealed class AppUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public short? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}
