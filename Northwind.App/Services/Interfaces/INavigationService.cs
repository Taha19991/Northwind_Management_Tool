using Northwind.App.ViewModels;

namespace Northwind.App.Services.Interfaces;

public interface INavigationService
{
    ViewModelBase? CurrentView { get; }
    event Action<ViewModelBase>? CurrentViewChanged;

    bool CanNavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    bool CanNavigateTo(Type viewModelType);
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateTo<TViewModel>(object? parameter) where TViewModel : ViewModelBase;
    void NavigateTo(Type viewModelType);
    void NavigateTo(Type viewModelType, object? parameter);
}
