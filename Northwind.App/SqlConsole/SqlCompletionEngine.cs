using System.Text.RegularExpressions;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.SqlConsole;

public enum SqlCompletionKind
{
    Table,
    View,
    Column,
    Keyword,
    Function
}

public sealed record SqlCompletionCandidate(
    string Text,
    SqlCompletionKind Kind,
    string Description,
    int SortOrder);

public sealed record SqlCompletionResult(
    int ReplaceStartOffset,
    int ReplaceLength,
    string Prefix,
    IReadOnlyList<SqlCompletionCandidate> Items);

public sealed class SqlCompletionEngine
{
    // Tolerant matcher for table/view references and optional aliases across the editor text.
    private static readonly Regex TableReferenceRegex = new(
        @"\b(?:from|join)\s+""?([a-zA-Z_][a-zA-Z0-9_]*)""?(?:\s+(?:as\s+)?""?([a-zA-Z_][a-zA-Z0-9_]*)""?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISqlCompletionSource _completionSource;

    public SqlCompletionEngine(ISqlCompletionSource completionSource)
    {
        _completionSource = completionSource;
    }

    public SqlCompletionResult GetCompletions(
        string fullText,
        int caretOffset,
        IReadOnlyList<SchemaObject> schema)
    {
        fullText ??= string.Empty;
        caretOffset = Math.Clamp(caretOffset, 0, fullText.Length);

        var wordStart = FindWordStart(fullText, caretOffset);
        var prefix = fullText[wordStart..caretOffset];
        var replaceLength = caretOffset - wordStart;

        var schemaByName = schema.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        // Rule 2: Member / alias access after '.' (e.g. c. or customers.)
        if (wordStart > 0 && fullText[wordStart - 1] == '.')
        {
            var qualifierEnd = wordStart - 1;
            var qualifierStart = FindWordStart(fullText, qualifierEnd);
            var qualifier = fullText[qualifierStart..qualifierEnd];

            if (!string.IsNullOrEmpty(qualifier))
            {
                var targetObject = ResolveQualifier(qualifier, fullText, schemaByName);
                if (targetObject is not null)
                {
                    var memberItems = targetObject.Columns
                        .Select((col, idx) => new SqlCompletionCandidate(
                            col.Name.ToLowerInvariant(),
                            SqlCompletionKind.Column,
                            FormatColumnDescription(targetObject.Name, col),
                            idx))
                        .ToList();

                    return new SqlCompletionResult(wordStart, replaceLength, prefix, memberItems);
                }

                return new SqlCompletionResult(wordStart, replaceLength, prefix, []);
            }
        }

        // Rule 1: After table-context trigger keywords -> tables & views only
        var previousWord = FindPreviousWord(fullText, wordStart);
        if (!string.IsNullOrEmpty(previousWord) &&
            _completionSource.TableContextTriggers.Contains(previousWord))
        {
            var objectItems = schema
                .Select((obj, idx) => new SqlCompletionCandidate(
                    obj.Name.ToLowerInvariant(),
                    obj.Kind == SchemaObjectKind.View ? SqlCompletionKind.View : SqlCompletionKind.Table,
                    FormatObjectDescription(obj),
                    idx))
                .ToList();

            return new SqlCompletionResult(wordStart, replaceLength, prefix, objectItems);
        }

        // Rule 3: General context -> keywords/functions, then columns of referenced tables/views, then all tables/views
        var items = new List<SqlCompletionCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sort = 0;

        foreach (var kw in _completionSource.Keywords)
        {
            if (seen.Add(kw.Text))
            {
                items.Add(new SqlCompletionCandidate(
                    kw.Text.ToUpperInvariant(),
                    kw.Kind == SqlKeywordKind.Function ? SqlCompletionKind.Function : SqlCompletionKind.Keyword,
                    kw.Description,
                    sort++));
            }
        }

        var statementText = ExtractCurrentStatement(fullText, caretOffset);
        var referencedObjects = FindReferencedObjects(statementText, schemaByName);
        foreach (var obj in referencedObjects)
        {
            foreach (var col in obj.Columns)
            {
                var colName = col.Name.ToLowerInvariant();
                if (seen.Add(colName))
                {
                    items.Add(new SqlCompletionCandidate(
                        colName,
                        SqlCompletionKind.Column,
                        FormatColumnDescription(obj.Name, col),
                        sort++));
                }
            }
        }

        foreach (var obj in schema)
        {
            var objName = obj.Name.ToLowerInvariant();
            if (seen.Add(objName))
            {
                items.Add(new SqlCompletionCandidate(
                    objName,
                    obj.Kind == SchemaObjectKind.View ? SqlCompletionKind.View : SqlCompletionKind.Table,
                    FormatObjectDescription(obj),
                    sort++));
            }
        }

        return new SqlCompletionResult(wordStart, replaceLength, prefix, items);
    }

    public static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private SchemaObject? ResolveQualifier(
        string qualifier,
        string fullText,
        Dictionary<string, SchemaObject> schemaByName)
    {
        if (schemaByName.TryGetValue(qualifier, out var directMatch))
        {
            return directMatch;
        }

        foreach (Match match in TableReferenceRegex.Matches(fullText))
        {
            var tableName = match.Groups[1].Value;
            var alias = match.Groups[2].Value;

            if (string.IsNullOrEmpty(alias) || _completionSource.ReservedClauseWords.Contains(alias))
            {
                continue;
            }

            if (string.Equals(alias, qualifier, StringComparison.OrdinalIgnoreCase) &&
                schemaByName.TryGetValue(tableName, out var aliasedObject))
            {
                return aliasedObject;
            }
        }

        return null;
    }

    private List<SchemaObject> FindReferencedObjects(
        string statementText,
        Dictionary<string, SchemaObject> schemaByName)
    {
        var objects = new List<SchemaObject>();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TableReferenceRegex.Matches(statementText))
        {
            var tableName = match.Groups[1].Value;
            if (schemaByName.TryGetValue(tableName, out var obj) && added.Add(obj.Name))
            {
                objects.Add(obj);
            }
        }

        return objects;
    }

    private static string ExtractCurrentStatement(string fullText, int caretOffset)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return string.Empty;
        }

        var start = fullText.LastIndexOf(';', Math.Max(0, caretOffset - 1));
        start = start < 0 ? 0 : start + 1;

        var end = fullText.IndexOf(';', caretOffset);
        if (end < 0)
        {
            end = fullText.Length;
        }

        return fullText[start..end];
    }

    private static int FindWordStart(string text, int offset)
    {
        var i = offset;
        while (i > 0 && IsIdentifierChar(text[i - 1]))
        {
            i--;
        }

        return i;
    }

    private static string FindPreviousWord(string text, int beforeOffset)
    {
        var i = beforeOffset;
        while (i > 0 && char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        var end = i;
        while (i > 0 && IsIdentifierChar(text[i - 1]))
        {
            i--;
        }

        return end > i ? text[i..end] : string.Empty;
    }

    private static string FormatObjectDescription(SchemaObject obj) =>
        $"{obj.Name} ({obj.KindLabel}, {obj.Columns.Count} columns)";

    private static string FormatColumnDescription(string tableName, SchemaColumn col)
    {
        var pkSuffix = col.IsPrimaryKey ? " [PK]" : string.Empty;
        return $"{tableName}.{col.Name} : {col.DataType}{pkSuffix}";
    }
}
