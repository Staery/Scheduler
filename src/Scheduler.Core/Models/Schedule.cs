using Scheduler.Core.Collections;

namespace Scheduler.Core.Models;

/// <summary>
/// An immutable set of events split into layers (timeline rows). Each layer is indexed by an
/// <see cref="AvlIntervalTree{T}"/>, so the events inside any time window are found in O(log n + k),
/// even when events overlap or share a start time.
/// </summary>
public sealed class Schedule
{
    private readonly AvlIntervalTree<ScheduleEvent>[] _trees;
    private readonly ScheduleEvent[][] _layers;

    public Schedule(IEnumerable<IEnumerable<ScheduleEvent>> layers, DateTime origin)
    {
        ArgumentNullException.ThrowIfNull(layers);

        _trees = layers.Select(layer =>
        {
            var tree = new AvlIntervalTree<ScheduleEvent>();
            foreach (var scheduleEvent in layer)
            {
                tree.Insert(scheduleEvent.Start, scheduleEvent.End, scheduleEvent);
            }

            return tree;
        }).ToArray();

        _layers = _trees.Select(tree => tree.InOrder().ToArray()).ToArray();
        Origin = origin;

        EventCount = _layers.Sum(layer => layer.Length);
        Length = _layers.SelectMany(layer => layer).Select(e => e.End).DefaultIfEmpty(0).Max();
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

    /// <summary>Height of the tallest layer index, for diagnostics.</summary>
    public int MaxTreeHeight => _trees.Select(tree => tree.Height).DefaultIfEmpty(0).Max();

    /// <summary>Events of a layer ordered by start.</summary>
    public IReadOnlyList<ScheduleEvent> GetLayer(int layer) => _layers[layer];

    /// <summary>Events of <paramref name="layer"/> that overlap the half-open window [from, to), ordered by start.</summary>
    public IEnumerable<ScheduleEvent> Query(int layer, double from, double to) =>
        layer < 0 || layer >= _trees.Length ? [] : _trees[layer].Query(from, to);

    /// <summary>The event of <paramref name="layer"/> at <paramref name="minute"/>, if any (the latest-starting one when events overlap).</summary>
    public ScheduleEvent? FindAt(int layer, double minute) =>
        Query(layer, minute, Math.BitIncrement(minute)).LastOrDefault();

    private int Count(EventStatus status) => _layers.Sum(layer => layer.Count(e => e.Status == status));
}
