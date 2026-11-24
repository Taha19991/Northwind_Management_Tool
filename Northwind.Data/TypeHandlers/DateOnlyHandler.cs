using System.Data;
using System.Globalization;
using Dapper;

namespace Northwind.Data.TypeHandlers;

/// <summary>
/// Dapper TypeHandler for <see cref="DateOnly"/>.
/// Although Dapper 2.1.x supports DateOnly natively, this handler is registered
/// to guarantee consistent PostgreSQL date mapping across versions.
/// </summary>
public sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly dateOnly => dateOnly,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException($"Cannot convert '{value.GetType().Name}' to DateOnly.")
    };
}
