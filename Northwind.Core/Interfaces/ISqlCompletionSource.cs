using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface ISqlCompletionSource
{
    IReadOnlyList<SqlKeywordItem> Keywords { get; }
    IReadOnlySet<string> TableContextTriggers { get; }
    IReadOnlySet<string> ReservedClauseWords { get; }
}
