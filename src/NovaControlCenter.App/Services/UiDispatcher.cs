using System.Windows;
using System.Windows.Threading;

namespace NovaControlCenter.App.Services;

/// <summary>Executa ações na thread da interface. Abstraído para manter os ViewModels testáveis.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    public void Post(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }
}
