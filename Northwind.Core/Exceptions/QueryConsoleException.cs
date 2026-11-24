namespace Northwind.Core.Exceptions;

public sealed class QueryConsoleException : Exception
{
    public string? SqlState { get; }
    public int? Position { get; }
    public string? Hint { get; }

    public QueryConsoleException(
        string message,
        string? sqlState = null,
        int? position = null,
        Exception? innerException = null,
        string? hint = null)
        : base(message, innerException)
    {
        SqlState = sqlState;
        Position = position is > 0 ? position : null;
        Hint = hint;
    }

    public string FormattedError
    {
        get
        {
            var text = string.IsNullOrWhiteSpace(SqlState)
                ? $"ERROR: {Message}"
                : $"ERROR {SqlState}: {Message}";

            return string.IsNullOrWhiteSpace(Hint) ? text : $"{text} Hint: {Hint}";
        }
    }
}
