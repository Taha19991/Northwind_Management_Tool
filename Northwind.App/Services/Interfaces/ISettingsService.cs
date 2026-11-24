namespace Northwind.App.Services.Interfaces;

public interface ISettingsService
{
    Task<DateOnly> GetReferenceDateAsync(CancellationToken ct = default);
}
