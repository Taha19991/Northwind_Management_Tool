using CommunityToolkit.Mvvm.ComponentModel;
using Northwind.Core.Exceptions;
using Npgsql;

namespace Northwind.App.ViewModels;

public abstract partial class ViewModelBase : ObservableValidator
{
    internal static Func<Exception, string>? ErrorTranslator { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task InitializeAsync(object? parameter) => InitializeAsync();

    protected async Task RunSafeAsync(Func<Task> action, bool allowConcurrent = false)
    {
        if (!allowConcurrent && IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await action();
        }
        catch (UserFriendlyException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (PostgresException ex)
        {
            ErrorMessage = ErrorTranslator?.Invoke(ex)
                ?? "An unexpected database error occurred.";
        }
        catch (NpgsqlException ex)
        {
            ErrorMessage = ErrorTranslator?.Invoke(ex)
                ?? "Cannot connect to the database. Check that PostgreSQL is running and the connection settings are correct.";
        }
        catch (OperationCanceledException)
        {
            // Operation canceled.
        }
        catch (Exception ex)
        {
            ErrorMessage = ErrorTranslator?.Invoke(ex) ?? ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
