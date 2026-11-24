using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Core.Interfaces;
using Northwind.Data.Repositories;

namespace Northwind.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddNorthwindData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddSingleton<IReadOnlyDbConnectionFactory, ReadOnlyDbConnectionFactory>();
        services.AddSingleton<ISqlCompletionSource, SqlKeywords>();
        services.AddSingleton<SqlLoader>();

        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddTransient<IEmployeeRepository, EmployeeRepository>();
        services.AddTransient<ILookupRepository, LookupRepository>();
        services.AddTransient<ITicketRepository, TicketRepository>();
        services.AddTransient<IReturnRepository, ReturnRepository>();
        services.AddTransient<IReportRepository, ReportRepository>();
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IQueryConsoleRepository, QueryConsoleRepository>();

        return services;
    }
}
