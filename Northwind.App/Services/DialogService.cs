using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Northwind.App.Services.Interfaces;
using Northwind.App.Views.Dialogs;
using Northwind.Core.Models;

namespace Northwind.App.Services;

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider? _serviceProvider;

    public DialogService(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public void ShowInfo(string message, string title = "Information")
    {
        RunOnUiThread(() => MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information));
    }

    public void ShowError(string message, string title = "Error")
    {
        RunOnUiThread(() => MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error));
    }

    public bool Confirm(string message, string title = "Confirm")
    {
        var result = MessageBoxResult.No;
        RunOnUiThread(() =>
        {
            result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        });
        return result == MessageBoxResult.Yes;
    }

    public bool ShowShipOrderDialog(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Service provider is not initialized for opening dialogs.");
        }

        var confirmed = false;
        RunOnUiThread(() =>
        {
            var dialog = _serviceProvider.GetRequiredService<ShipOrderDialog>();
            dialog.Owner = Application.Current?.MainWindow;
            dialog.ViewModel.LoadOrder(order);
            confirmed = dialog.ShowDialog() == true;
        });

        return confirmed;
    }

    public int? ShowNewTicketDialog()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Service provider is not initialized for opening dialogs.");
        }

        int? createdTicketId = null;
        RunOnUiThread(() =>
        {
            var dialog = _serviceProvider.GetRequiredService<NewTicketDialog>();
            dialog.Owner = Application.Current?.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                createdTicketId = dialog.ViewModel.CreatedTicketId;
            }
        });

        return createdTicketId;
    }

    public int? ShowNewReturnDialog()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Service provider is not initialized for opening dialogs.");
        }

        int? createdReturnId = null;
        RunOnUiThread(() =>
        {
            var dialog = _serviceProvider.GetRequiredService<NewReturnDialog>();
            dialog.Owner = Application.Current?.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                createdReturnId = dialog.ViewModel.CreatedReturnId;
            }
        });

        return createdReturnId;
    }

    public void ShowSqlDialog(string title, string sqlText)
    {
        RunOnUiThread(() =>
        {
            var dialog = new SqlPreviewDialog(title, sqlText)
            {
                Owner = Application.Current?.MainWindow
            };
            dialog.ShowDialog();
        });
    }

    public string? ShowSaveCsvDialog(string defaultFileName)
    {
        string? filePath = null;
        RunOnUiThread(() =>
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Report to CSV",
                Filter = "CSV File (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = defaultFileName
            };

            if (saveDialog.ShowDialog(Application.Current?.MainWindow) == true)
            {
                filePath = saveDialog.FileName;
            }
        });
        return filePath;
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}
