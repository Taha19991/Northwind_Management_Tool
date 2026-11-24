using System.Globalization;
using Microsoft.Extensions.Configuration;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Exceptions;
using Northwind.Core.Interfaces;

namespace Northwind.App.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IConfiguration _configuration;
    private readonly IReportRepository _reportRepository;

    public SettingsService(IConfiguration configuration, IReportRepository reportRepository)
    {
        _configuration = configuration;
        _reportRepository = reportRepository;
    }

    public async Task<DateOnly> GetReferenceDateAsync(CancellationToken ct = default)
    {
        var configured = _configuration["Reports:ReferenceDate"]?.Trim();
        if (string.IsNullOrEmpty(configured) ||
            string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return await _reportRepository.GetReferenceDateAsync(ct);
        }

        if (DateOnly.TryParseExact(configured, AppFormats.IsoDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
        {
            return exactDate;
        }

        if (DateOnly.TryParse(configured, CultureInfo.CurrentCulture, DateTimeStyles.None, out var fallbackDate))
        {
            return fallbackDate;
        }

        throw new UserFriendlyException(
            $"Invalid Reports:ReferenceDate setting: '{configured}'. Use 'auto' or 'yyyy-MM-dd'.");
    }
}
