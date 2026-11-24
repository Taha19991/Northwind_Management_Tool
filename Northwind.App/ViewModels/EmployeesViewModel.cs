using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed partial class EmployeesViewModel : ViewModelBase
{
    private readonly IEmployeeRepository _employeeRepository;
    private CancellationTokenSource? _territoryCts;

    public EmployeesViewModel(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public ObservableCollection<EmployeeNodeViewModel> RootNodes { get; } = [];

    public ObservableCollection<Territory> Territories { get; } = [];

    [ObservableProperty]
    private EmployeeNodeViewModel? _activeEmployee;

    [ObservableProperty]
    private int _totalEmployees;

    [ObservableProperty]
    private bool _isLoadingTerritories;

    public override Task InitializeAsync() => LoadEmployeesAsync();

    [RelayCommand]
    private Task LoadEmployeesAsync() => RunSafeAsync(async () =>
    {
        var employees = await _employeeRepository.GetAllAsync();
        TotalEmployees = employees.Count;

        var byId = new Dictionary<short, Employee>(employees.Count);
        foreach (var emp in employees)
        {
            byId[emp.EmployeeId] = emp;
        }

        var nodesById = new Dictionary<short, EmployeeNodeViewModel>(employees.Count);
        foreach (var emp in employees)
        {
            var managerName = emp.ReportsTo.HasValue && byId.TryGetValue(emp.ReportsTo.Value, out var manager)
                ? manager.FullName
                : "-";
            nodesById[emp.EmployeeId] = new EmployeeNodeViewModel(emp, managerName);
        }

        RootNodes.Clear();
        foreach (var emp in employees)
        {
            var node = nodesById[emp.EmployeeId];
            if (emp.ReportsTo.HasValue && nodesById.TryGetValue(emp.ReportsTo.Value, out var parentNode))
            {
                parentNode.DirectReports.Add(node);
            }
            else
            {
                RootNodes.Add(node);
            }
        }

        if (RootNodes.Count > 0)
        {
            RootNodes[0].IsCurrent = true;
            ActiveEmployee = RootNodes[0];
        }
        else
        {
            ActiveEmployee = null;
            Territories.Clear();
        }
    });

    partial void OnActiveEmployeeChanged(EmployeeNodeViewModel? value)
    {
        if (value is null)
        {
            Territories.Clear();
            return;
        }

        _ = LoadTerritoriesAsync(value.Employee.EmployeeId);
    }

    private async Task LoadTerritoriesAsync(short employeeId)
    {
        _territoryCts?.Cancel();
        _territoryCts?.Dispose();
        _territoryCts = new CancellationTokenSource();
        var token = _territoryCts.Token;

        IsLoadingTerritories = true;
        try
        {
            var items = await _employeeRepository.GetTerritoriesAsync(employeeId, token);
            token.ThrowIfCancellationRequested();

            Territories.Clear();
            foreach (var territory in items)
            {
                Territories.Add(territory);
            }
        }
        catch (OperationCanceledException)
        {
            // Another employee was chosen.
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingTerritories = false;
        }
    }
}
