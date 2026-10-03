using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Scheduler.Converters;

/// <summary>
/// Visible for <see langword="true"/>, non-empty strings, non-zero numbers and other non-null values;
/// collapsed otherwise. Set <see cref="Invert"/> to flip the result.
/// </summary>
public sealed class VisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool flag => flag,
            string text => text.Length > 0,
            int number => number != 0,
            _ => true,
        };

        return visible ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
