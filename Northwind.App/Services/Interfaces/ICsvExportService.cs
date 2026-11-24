using System.Collections;
using System.Data;

namespace Northwind.App.Services.Interfaces;

public interface ICsvExportService
{
    Task ExportAsync<T>(IEnumerable<T> items, string filePath, CancellationToken ct = default);
    Task ExportAsync(IEnumerable items, Type itemType, string filePath, CancellationToken ct = default);
    Task ExportDataTableAsync(DataTable table, string filePath, CancellationToken ct = default);
}
