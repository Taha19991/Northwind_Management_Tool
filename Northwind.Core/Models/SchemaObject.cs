namespace Northwind.Core.Models;

public enum SchemaObjectKind
{
    Table,
    View
}

public sealed class SchemaObject
{
    public string Name { get; set; } = string.Empty;
    public SchemaObjectKind Kind { get; set; }
    public List<SchemaColumn> Columns { get; set; } = [];

    public string KindLabel => Kind == SchemaObjectKind.View ? "view" : "table";
}
