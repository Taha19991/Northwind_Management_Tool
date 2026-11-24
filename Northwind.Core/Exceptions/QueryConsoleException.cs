namespace Northwind.Core.Exceptions;

public sealed class QueryConsoleException : Exception
{
    public string? SqlState { get; }
    public int? Position { get; }

    public QueryConsoleException(
        string message,
        string? sqlState = null,
        int? position = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        SqlState = sqlState;
        Position = position is > 0 ? position : null;
    }

    public string FormattedError => string.IsNullOrWhiteSpace(SqlState)
        ? $"ERROR: {Message}"
        : $"ERROR {SqlState}: {Message}";
}
