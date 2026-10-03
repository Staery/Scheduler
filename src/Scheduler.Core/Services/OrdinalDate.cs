using System.Globalization;

namespace Scheduler.Core.Services;

/// <summary>Formats dates like "October 3rd, Saturday".</summary>
public static class OrdinalDate
{
    public static string Format(DateTime date)
    {
        var culture = CultureInfo.InvariantCulture;
        return $"{date.ToString("MMMM", culture)} {date.Day}{Suffix(date.Day)}, {date.ToString("dddd", culture)}";
    }

    public static string Suffix(int day) => (day % 100) switch
    {
        11 or 12 or 13 => "th",
        _ => (day % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        },
    };
}
