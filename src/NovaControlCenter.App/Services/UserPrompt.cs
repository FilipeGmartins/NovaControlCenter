using System.Windows;

namespace NovaControlCenter.App.Services;

/// <summary>Confirmações ao usuário. Abstraído para manter os ViewModels testáveis.</summary>
public interface IUserPrompt
{
    bool Confirm(string message, string title);
}

public sealed class MessageBoxUserPrompt : IUserPrompt
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(Application.Current.MainWindow!, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
        == MessageBoxResult.Yes;
}
