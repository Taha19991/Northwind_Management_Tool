using Northwind.Core.Models;

namespace Northwind.App.Services.Interfaces;

public interface IDialogService
{
    void ShowInfo(string message, string title = "Information");
    void ShowError(string message, string title = "Error");
    bool Confirm(string message, string title = "Confirm");
    bool ShowShipOrderDialog(Order order);
    int? ShowNewTicketDialog();
    int? ShowNewReturnDialog();
    void ShowSqlDialog(string title, string sqlText);
    string? ShowSaveCsvDialog(string defaultFileName);
}
