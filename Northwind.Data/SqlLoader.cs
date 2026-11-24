using System.Collections.Concurrent;
using System.Reflection;

namespace Northwind.Data;

public sealed class SqlLoader
{
    private const string ReportResourcePrefix = "Northwind.Data.Sql.Reports.";
    private readonly Assembly _assembly = typeof(SqlLoader).Assembly;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string Load(string reportKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportKey);

        var normalizedKey = reportKey.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            ? reportKey[..^4]
            : reportKey;

        return _cache.GetOrAdd(normalizedKey, key =>
        {
            var resourceName = $"{ReportResourcePrefix}{key}.sql";
            using var stream = _assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException(
                    $"Embedded SQL resource not found: '{resourceName}'.", resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }
}
