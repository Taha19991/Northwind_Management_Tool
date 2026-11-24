using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;

namespace Northwind.App.Services;

public sealed class CsvExportService : ICsvExportService
{
    private readonly IConfiguration _configuration;

    public CsvExportService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task ExportAsync<T>(IEnumerable<T> items, string filePath, CancellationToken ct = default) =>
        ExportAsync(items, typeof(T), filePath, ct);

    public async Task ExportAsync(IEnumerable items, Type itemType, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemType);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var (culture, separator) = ResolveCsvFormat();
        var properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var isCurrency = new bool[properties.Length];
        var builder = new StringBuilder();

        for (var i = 0; i < properties.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(separator);
            }

            var displayAttr = properties[i].GetCustomAttribute<DisplayAttribute>();
            var header = !string.IsNullOrWhiteSpace(displayAttr?.Name)
                ? displayAttr.Name
                : properties[i].Name;

            var dataTypeAttr = properties[i].GetCustomAttribute<DataTypeAttribute>();
            isCurrency[i] = dataTypeAttr?.DataType == DataType.Currency;

            builder.Append(EscapeCell(header, separator));
        }
        builder.AppendLine();

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            for (var i = 0; i < properties.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }

                var rawValue = item is null ? null : properties[i].GetValue(item);
                var text = FormatValue(rawValue, isCurrency[i], culture);
                builder.Append(EscapeCell(text, separator));
            }
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(
            filePath,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            ct);
    }

    public async Task ExportDataTableAsync(DataTable table, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var (culture, separator) = ResolveCsvFormat();
        var builder = new StringBuilder();

        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(separator);
            }

            builder.Append(EscapeCell(table.Columns[i].ColumnName, separator));
        }
        builder.AppendLine();

        foreach (DataRow row in table.Rows)
        {
            ct.ThrowIfCancellationRequested();

            for (var i = 0; i < table.Columns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }

                var rawValue = row[i];
                var text = rawValue is null or DBNull
                    ? string.Empty
                    : FormatValue(rawValue, isCurrency: false, culture);
                builder.Append(EscapeCell(text, separator));
            }
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(
            filePath,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            ct);
    }

    private (CultureInfo Culture, char Separator) ResolveCsvFormat()
    {
        var configured = _configuration["Export:CsvCulture"]?.Trim();
        var cultureName = string.IsNullOrEmpty(configured) ? "en-US" : configured;
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var separator = culture.NumberFormat.NumberDecimalSeparator == "," ? ';' : ',';
        return (culture, separator);
    }

    private static string FormatValue(object? rawValue, bool isCurrency, CultureInfo culture) => rawValue switch
    {
        null => string.Empty,
        decimal d => isCurrency ? d.ToString("F2", culture) : d.ToString("G29", culture),
        DateOnly date => date.ToString(AppFormats.IsoDate, CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString(AppFormats.IsoDateTime, CultureInfo.InvariantCulture),
        TicketCategory tc => EnumText.ToDisplay(tc),
        TicketStatus ts => EnumText.ToDisplay(ts),
        TicketPriority tp => EnumText.ToDisplay(tp),
        ReturnReason rr => EnumText.ToDisplay(rr),
        ReturnStatus rs => EnumText.ToDisplay(rs),
        OrderStatus os => EnumText.ToDisplay(os),
        UserRole ur => EnumText.ToDisplay(ur),
        IFormattable formattable => formattable.ToString(null, culture),
        _ => rawValue.ToString() ?? string.Empty
    };

    private static string EscapeCell(string value, char separator)
    {
        if (value.Contains(separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
