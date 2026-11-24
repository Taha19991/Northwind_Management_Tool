using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed partial class EmployeeNodeViewModel : ObservableObject
{
    public EmployeeNodeViewModel(Employee employee, string managerName)
    {
        Employee = employee;
        ManagerName = managerName;
    }

    public Employee Employee { get; }

    public string FullName => Employee.FullName;

    public string Title => Employee.Title ?? string.Empty;

    public string ManagerName { get; }

    public ObservableCollection<EmployeeNodeViewModel> DirectReports { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isCurrent;
}
