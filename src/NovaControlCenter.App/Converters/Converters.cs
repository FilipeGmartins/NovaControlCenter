using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NovaControlCenter.App.Presentation;

namespace NovaControlCenter.App.Converters;

/// <summary>StatusKind → pincel do tema (Success / Warning / Danger).</summary>
public sealed class StatusKindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is StatusKind kind
            ? kind switch { StatusKind.Ok => "Success", StatusKind.Warning => "Warning", _ => "Danger" }
            : "TextMuted";
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>StatusKind → glifo Segoe MDL2.</summary>
public sealed class StatusKindToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        StatusKind.Ok => "",      // check
        StatusKind.Warning => "", // aviso
        _ => "",                  // X
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>bool → Visibility, com inversão opcional via ConverterParameter="Invert".</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is "Invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
