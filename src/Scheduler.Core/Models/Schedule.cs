namespace Scheduler.Core.Models;

/// <summary>
/// An immutable set of events split into layers (timeline rows). Events of a layer never overlap and are sorted by
/// start, so the events in any time window are found with a binary search: O(log n + k) per layer.
/// </summary>
public sealed class Schedule
{
    private readonly ScheduleEvent[][] _layers;

    public Schedule(IEnumerable<IEnumerable<ScheduleEvent>> layers, DateTime origin)
    {
        ArgumentNullException.ThrowIfNull(layers);

        _layers = layers.Select(layer => layer.OrderBy(e => e.Start).ToArray()).ToArray();
        Origin = origin;

        foreach (var layer in _layers)
        {
            for (var i = 1; i < layer.Length; i++)
            {
                if (layer[i].Start < layer[i - 1].End)
                {
                    throw new ArgumentException($"Events {layer[i - 1].Id} and {layer[i].Id} overlap in the same layer.", nameof(layers));
                }
            }
        }

        EventCount = _layers.Sum(layer => layer.Length);
        Length = _layers.Select(layer => layer.Length == 0 ? 0 : layer[^1].End).DefaultIfEmpty(0).Max();
        PendingCount = Count(EventStatus.Pending);
        JeopardyCount = Count(EventStatus.Jeopardy);
        CompletedCount = Count(EventStatus.Completed);
    }

    public static Schedule Empty { get; } = new([], DateTime.Today);

    /// <summary>Wall-clock time of minute 0.</summary>
    public DateTime Origin { get; }

    public int LayerCount => _layers.Length;

    public int EventCount { get; }

    /// <summary>End of the last event, in minutes.</summary>
    public int Length { get; }

    public int PendingCount { get; }

    public int JeopardyCount { get; }

    public int CompletedCount { get; }

    public IReadOnlyList<ScheduleEvent> GetLayer(int layer) => _layers[layer];

    /// <summary>Events of <paramref name="layer"/> that overlap the half-open window [from, to).</summary>
    public IEnumerable<ScheduleEvent> Query(int layer, double from, double to)
    {
        if (layer < 0 || layer >= _layers.Length || to <= from)
        {
            yield break;
        }

        var events = _layers[layer];
        for (var i = FirstEndingAfter(events, from); i < events.Length && events[i].Start < to; i++)
        {
            yield return events[i];
        }
    }

    /// <summary>The event of <paramref name="layer"/> at <paramref name="minute"/>, if any.</summary>
    public ScheduleEvent? FindAt(int layer, double minute)
    {
        if (layer < 0 || layer >= _layers.Length)
        {
            return null;
        }

        var events = _layers[layer];
        var index = FirstEndingAfter(events, minute);
        return index < events.Length && events[index].Start <= minute ? events[index] : null;
    }

    private int Count(EventStatus status) => _layers.Sum(layer => layer.Count(e => e.Status == status));

    /// <summary>Index of the first event whose end is after <paramref name="minute"/>; ends are sorted because events do not overlap.</summary>
    private static int FirstEndingAfter(ScheduleEvent[] events, double minute)
    {
        int low = 0, high = events.Length;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (events[mid].End <= minute)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
