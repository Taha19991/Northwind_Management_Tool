namespace Northwind.Core.Models;

public enum SqlKeywordKind
{
    Keyword,
    Function
}

public sealed record SqlKeywordItem(string Text, SqlKeywordKind Kind, string Description);
