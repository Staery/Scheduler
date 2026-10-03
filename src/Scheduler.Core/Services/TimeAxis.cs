using System.Globalization;

namespace Scheduler.Core.Services;

/// <summary>Chooses readable ruler steps and labels for a zoom level.</summary>
public static class TimeAxis
{
    /// <summary>Candidate steps in minutes, from 5 minutes to a week.</summary>
    private static readonly int[] Steps = [5, 10, 15, 30, 60, 120, 180, 360, 720, 1440, 2880, 10080];

    /// <summary>Smallest step that keeps labels at least <paramref name="minPixels"/> apart.</summary>
    public static int StepFor(double pixelsPerMinute, double minPixels = 80)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerMinute);

        foreach (var step in Steps)
        {
            if (step * pixelsPerMinute >= minPixels)
            {
                return step;
            }
        }

        return Steps[^1];
    }

    /// <summary>Tick positions (minutes) covering [from, to], aligned to the step.</summary>
    public static IEnumerable<int> Ticks(double from, double to, int step)
    {
        var first = (int)Math.Floor(Math.Max(0, from) / step) * step;
        for (var tick = first; tick <= to; tick += step)
        {
            yield return tick;
        }
    }

    /// <summary>"08:30", or "Tue 4 · 08:30" when the tick starts a new day or the step is a day or more.</summary>
    public static string Label(DateTime origin, int minute, int step)
    {
        var time = origin.AddMinutes(minute);
        var culture = CultureInfo.InvariantCulture;

        if (step >= 1440)
        {
            return time.ToString("ddd d MMM", culture);
        }

        return time.TimeOfDay == TimeSpan.Zero
            ? time.ToString("ddd d · HH:mm", culture)
            : time.ToString("HH:mm", culture);
    }
}
