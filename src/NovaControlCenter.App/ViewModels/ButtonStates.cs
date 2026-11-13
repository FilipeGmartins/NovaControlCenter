using CommunityToolkit.Mvvm.ComponentModel;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.App.ViewModels;

/// <summary>
/// Estado dos botões exposto por indexador para o XAML: <c>{Binding Buttons[A]}</c>.
/// Notifica "Item[]" apenas quando algum botão muda.
/// </summary>
public sealed class ButtonStates : ObservableObject
{
    public static readonly GamepadButtons[] All =
        Enum.GetValues<GamepadButtons>().Where(b => b != GamepadButtons.None).ToArray();

    private GamepadButtons _value;

    public GamepadButtons Value => _value;

    public bool this[string name] =>
        Enum.TryParse<GamepadButtons>(name, out var button) && (_value & button) == button;

    public int Count => All.Count(b => (_value & b) == b);

    public void Set(GamepadButtons value)
    {
        if (value == _value) return;
        _value = value;
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Value));
    }

    public void Add(GamepadButtons value) => Set(_value | value);
}
