using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface IEmployeeRepository
{
    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Territory>> GetTerritoriesAsync(short employeeId, CancellationToken ct = default);
}
