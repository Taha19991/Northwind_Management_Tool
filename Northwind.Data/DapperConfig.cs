using Dapper;
using Northwind.Core.Enums;
using Northwind.Data.TypeHandlers;

namespace Northwind.Data;

public static class DapperConfig
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        DefaultTypeMap.MatchNamesWithUnderscores = true;

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new EnumTextTypeHandler<UserRole>());
        SqlMapper.AddTypeHandler(new EnumTextTypeHandler<TicketStatus>());
        SqlMapper.AddTypeHandler(new EnumTextTypeHandler<TicketCategory>());
        SqlMapper.AddTypeHandler(new EnumTextTypeHandler<TicketPriority>());
        SqlMapper.AddTypeHandler(new EnumTextTypeHandler<ReturnStatus>());
        SqlMapper.AddTypeHandler(new EnumTextTypeHandler<ReturnReason>());
    }
}
