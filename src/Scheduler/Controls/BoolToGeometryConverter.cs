using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Scheduler.Controls;

/// <summary>Chooses one of two geometries, e.g. a play or a pause icon.</summary>
public sealed class BoolToGeometryConverter : IValueConverter
{
    public Geometry? True { get; set; }

    public Geometry? False { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? True : False;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
