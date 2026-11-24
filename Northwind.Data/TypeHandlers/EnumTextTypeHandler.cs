using System.Data;
using Dapper;
using Northwind.Core.Enums;

namespace Northwind.Data.TypeHandlers;

public sealed class EnumTextTypeHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
    where TEnum : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, TEnum value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = EnumText.ToDb(value);
    }

    public override TEnum Parse(object value) => value switch
    {
        TEnum enumValue => enumValue,
        string text => EnumText.FromDb<TEnum>(text),
        _ => EnumText.FromDb<TEnum>(value.ToString() ?? string.Empty)
    };
}
