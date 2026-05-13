using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Mabipacade.DebugUi.ViewModels;

namespace Mabipacade.DebugUi.Views;

public sealed class ModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SourceMode m && parameter is string s && Enum.TryParse<SourceMode>(s, out var p) && m == p;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string s && Enum.TryParse<SourceMode>(s, out var p)
            ? p
            : Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
