using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Northwind.App.Services;
using Northwind.App.Services.Interfaces;
using Northwind.App.ViewModels;
using Northwind.App.ViewModels.Dialogs;
using Northwind.App.Views;
using Northwind.App.Views.Dialogs;
using Northwind.Core.Enums;
using Northwind.Core.Exceptions;
using Northwind.Data;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnLastWindowClose;
        ConfigureCulture();
        DapperConfig.Configure();
        ViewModelBase.ErrorTranslator = PostgresErrorMapper.ToUserMessage;

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((_, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddNorthwindData(context.Configuration);
                    ConfigureAppServices(services);
                })
                .Build();

            await _host.StartAsync();
            ShowLoginWindow();
        }
        catch (Exception ex)
        {
            var dialogService = new DialogService();
            dialogService.ShowError(
                $"An error occurred while starting the application:\n{FormatExceptionMessage(ex)}",
                "Northwind Management Panel");
            Shutdown(-1);
        }
    }

    private static void ConfigureAppServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ICsvExportService, CsvExportService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<NavigationService>(sp =>
        {
            var nav = new NavigationService(
                sp,
                sp.GetRequiredService<ISessionService>(),
                sp.GetRequiredService<IDialogService>());

            nav.RegisterRoutePolicy<DashboardViewModel>(UserRole.Manager);
            nav.RegisterRoutePolicy<CustomersViewModel>(UserRole.Manager, UserRole.Sales, UserRole.Support);
            nav.RegisterRoutePolicy<ProductsViewModel>(UserRole.Manager, UserRole.Sales, UserRole.Logistics);
            nav.RegisterRoutePolicy<OrdersViewModel>(UserRole.Manager, UserRole.Sales);
            nav.RegisterRoutePolicy<OrderEditorViewModel>(UserRole.Manager, UserRole.Sales);
            nav.RegisterRoutePolicy<ShippingViewModel>(UserRole.Manager, UserRole.Logistics);
            nav.RegisterRoutePolicy<TicketsViewModel>(UserRole.Manager, UserRole.Support);
            nav.RegisterRoutePolicy<ReturnsViewModel>(UserRole.Manager, UserRole.Logistics, UserRole.Support);
            nav.RegisterRoutePolicy<EmployeesViewModel>(UserRole.Manager);
            nav.RegisterRoutePolicy<ReportsViewModel>(UserRole.Manager);
            nav.RegisterRoutePolicy<SqlConsoleViewModel>(UserRole.Manager);

            return nav;
        });
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<OrdersViewModel>();
        services.AddTransient<OrderEditorViewModel>();
        services.AddTransient<ShippingViewModel>();
        services.AddTransient<TicketsViewModel>();
        services.AddTransient<ReturnsViewModel>();
        services.AddTransient<EmployeesViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SqlConsoleViewModel>();
        services.AddTransient<ShipOrderDialogViewModel>();
        services.AddTransient<NewTicketDialogViewModel>();
        services.AddTransient<NewReturnDialogViewModel>();

        // Windows and Dialogs
        services.AddTransient<LoginWindow>();
        services.AddTransient<ShellWindow>();
        services.AddTransient<ShipOrderDialog>();
        services.AddTransient<NewTicketDialog>();
        services.AddTransient<NewReturnDialog>();
    }

    private void ShowLoginWindow()
    {
        if (_host is null)
        {
            return;
        }

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        loginWindow.ViewModel.LoginSucceeded += () =>
        {
            ShowShellWindow();
            loginWindow.Close();
        };

        MainWindow = loginWindow;
        loginWindow.Show();
    }

    private void ShowShellWindow()
    {
        if (_host is null)
        {
            return;
        }

        var shellWindow = _host.Services.GetRequiredService<ShellWindow>();
        shellWindow.ViewModel.LogoutRequested += () =>
        {
            ShowLoginWindow();
            shellWindow.Close();
        };

        MainWindow = shellWindow;
        shellWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var dialogService = _host?.Services.GetService<IDialogService>() ?? new DialogService();
        dialogService.ShowError(
            $"An unexpected error occurred:\n{FormatExceptionMessage(e.Exception)}",
            "Northwind Management Panel");

        e.Handled = true;
    }

    private static string FormatExceptionMessage(Exception exception) => exception switch
    {
        UserFriendlyException ufe => ufe.Message,
        NpgsqlException => PostgresErrorMapper.ToUserMessage(exception),
        _ => exception.Message
    };

    private static void ConfigureCulture()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-US")));
    }
}
